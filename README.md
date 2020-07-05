# function-hacks

Some hacky scripts I've thrown together to help me explore some aspects of azure function behaviour, specifically when they timeout.

#### The Azure function

I have the following azure function (below) deployed at url `https://REDACTED_AZUREFUNCTION_URL.azurewebsites.net/api/Test1?name=fred&code=REDACTED_API_FUNCTION_KEY` which is secured with a function key, and the key and domain are stored in Linqpad password manager.

* [extract from test-azure-function-timeout.linq](test-azure-function-timeout.linq)

```csharp
 public static long _cnt = 0;
        [FunctionName("Test1")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            var cnt = System.Threading.Interlocked.Increment(ref _cnt);
            log.LogInformation($"C# HTTP trigger function processed request {cnt:000}.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? $"This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response. [{cnt}]"
                : $"Hello, {name}. This HTTP triggered function executed successfully. [{cnt}]";

            return new OkObjectResult(responseMessage);
        }
    }
```

That function is what you get when you create a new AzureFunction using the wizard in Visual studio 2019.

#### hacky Test to see how long it takes to times out

```csharp

async Task Run()
{
	Console.WriteLine("");
	Console.WriteLine("testing how long it takes for azure functions to timeout");
	Console.WriteLine("--------------------------------------------------------");
	int last = -1;
	// lets start at 10 seconds, and increment by 10% on each request
	double delay = 60; 
	bool reset = false;
	while(!reset) {
		(int cnt, double elapsedMS) = await GetNumber();
		Console.WriteLine($"paused {(int)delay,-6} seconds. Total = [{cnt}] {elapsedMS,10}");
		if(cnt < last) reset = true;
		last = cnt;
		delay *= 1.1;
		await Task.Delay((int)(delay * 1000));
	}
	Console.WriteLine("-------------------------------------");
	Console.WriteLine($"We have a winner at {delay} seconds.");
	Console.WriteLine("-------------------------------------");
}


public static async Task<(int, double)> GetNumber()
{
	sw.Reset();
	sw.Start();
	var response = await client.GetAsync(azureFunctionUrl);
	sw.Stop();
	double elapsedMS = sw.Elapsed.TotalMilliseconds;
	var text = await response.Content.ReadAsStringAsync();
	var re = new System.Text.RegularExpressions.Regex(@"\[(.*)?\]").Match(text).Groups[1].Value;
	int num = int.Parse(re);
	return (num, elapsedMS);
}
```

#### hacky test to test concurrency issues

***notes***

- I am doing a 'get' and not a POST, so in theory it is OK for the network to cache the result. No cache-buster added to the end of the `get` url.
- I have not specified that the AZ functio should be a `Singleton` so I expect that under extreme load, or (actually any condition that Microsoft may deem appropriate and without warning) could in theory host multiple instances of my function, so this script is what i am playing with to see if I can get Azure to do this?
- I am confirming concurrency is not an issue by making as many parrallel requests as possible, and checking that the sorted list of returned values have no duplicates, and no values are skipped. ***of course, values returned return out of sequence, but that's expected and not a failure, as long as there are no duplicates or gaps after eventual consistency***


***musings?***

- i have not tested firing up this linqpad script in different regions to see if Microsoft would route the traffic to the alraedy running single instance, or create a new instance? i.e. spare capacity et al.

**[extract from : messing-around-with-azure-functions.linq](messing-around-with-azure-functions.linq)**

```csharp


static async Task Main(string[] args)
{
	// get secrets
	
	
	SelfTest(); 	
	await RunTest(30, 2);
	return;
	await RunTest(30, 20);
	await RunTest(30, 30);
	await RunTest(100, 50);
}

public static async Task RunTest(int cnt, int threads) {
	var nums = await DoIt(cnt, threads);
	checkGapsAndDuplicates(nums);
}


public static (int[] gaps, Dups[] duplicates) checkGapsAndDuplicates(IEnumerable<int> src)
{
	var ordered = src.OrderBy(o => o).ToArray();
	var first = ordered[0];
	var last = ordered[ordered.Length - 1];

	int cnt = ordered.Length;
	var seq = Enumerable.Range(first, cnt).Select(i => i);
	var gaps = seq.Except(ordered).ToArray();
	var duplicates =  ordered.GroupBy(o => o).Where(o => o.Count() > 1).Select(o => new Dups(o.Key, o.Count())).ToArray();
	return (gaps, duplicates);
}

static async Task<int[]> DoIt(int cnt, int threads)
{
	var sw = new Stopwatch();
	sw.Start();	
	var results = await Run(cnt, threads);
	sw.Stop();
	double elapsedSeconds = sw.Elapsed.TotalSeconds;
	double rps = ((double)_totalRequests) / elapsedSeconds;
	Console.WriteLine($"independant count check, total [{_totalRequests}] requests in [{elapsedSeconds:0.000}] seconds. [{rps:0.0}]rps");
	return results.SelectMany(r => r).ToArray();
}
```

linqpad scripts:

* [test accessing Azure Function in parallel](messing-around-with-azure-functions.linq)
* [test how long azure function 'state' remains active](test-azure-function-timeout.linq)



