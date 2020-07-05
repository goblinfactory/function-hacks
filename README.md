# function-hacks

Some hacky scripts I've thrown together to help me explore some aspects of azure function behaviour, specifically when they timeout.

#### The Azure function

I have the following azure function (below) deployed at url `https://REDACTED_AZUREFUNCTION_URL.azurewebsites.net/api/Test1?name=fred&code=REDACTED_API_FUNCTION_KEY` which is secured with a function key, and the key and domain are stored in Linqpad password manager.

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

* [extract from test-azure-function-timeout.linq](test-azure-function-timeout.linq)

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
**Resulting Winner**

running the code above (with two runs) gives us the following results; `333` and `403` seconds respectively.

<img src='https://github.com/goblinfactory/function-hacks/blob/master/docs/TimeoutWinner.png' width='500px' />

**Resulting distribution, request (server time) in ms for this test**

<img src="https://github.com/goblinfactory/function-hacks/blob/master/docs/TestTimeout.PNG" width='700px'/>



#### hacky test to test concurrency issues

***notes***

- I am doing a 'get' and not a POST, so in theory it is OK for the network to cache the result. No cache-buster added to the end of the `get` url.
- I have not specified that the AZ functio should be a `Singleton` so I expect that under extreme load, or (actually any condition that Microsoft may deem appropriate and without warning) could in theory host multiple instances of my function, so this script is what i am playing with to see if I can get Azure to do this?
- I am confirming concurrency is not an issue by making as many parrallel requests as possible, and checking that the sorted list of returned values have no duplicates, and no values are skipped. ***of course, values returned return out of sequence, but that's expected and not a failure, as long as there are no duplicates or gaps after eventual consistency***


***musings?***

- i have not tested firing up this linqpad script in different regions to see if Microsoft would route the traffic to the alraedy running single instance, or create a new instance? i.e. spare capacity et al.
- what's the simplest optimistic concurrency lock mechanism I can add to an azure function to tell if another function is live?
	- that gives some obvious challenges
		- how to tell an existing function... "go away", and still service clients effectively? (I doubt that will even be possible.) throw enough 500's and Azure would possibly simply spin up another server and the problem simply dominoes?

***todo***

- I need to show my cost estimates before running these tests. I did not want to blow my £1 budget for running all my tests!
- I still need to run these from a VM and from a build agent from within Azure itself, configured with values so that the test will complete within around 30 seconds. 

**Random result from running on a VM on a mac, at home, across home broadband**

	10 items,     1 threads : independant count check, total [   10] requests in [0.641] seconds. [    15.6]rps
	80 items,     2 threads : independant count check, total [   90] requests in [1.847] seconds. [    48.7]rps
	40 items,     4 threads : independant count check, total [  130] requests in [0.544] seconds. [   238.9]rps
	20 items,     8 threads : independant count check, total [  150] requests in [0.231] seconds. [   649.0]rps
	40 items,     8 threads : independant count check, total [  190] requests in [0.405] seconds. [   469.6]rps
	80 items,     8 threads : independant count check, total [  270] requests in [0.522] seconds. [   517.3]rps

**Resulting distribution, request (server time) in ms for this test**

(consider this as the cost for doing nothing, and costs go up from here. This is effectively the baseline of doing "nothing" in .NET. (incrementing one number)

<img src='https://github.com/goblinfactory/function-hacks/blob/master/docs/TestConcurrency.PNG' width='700px'/>

**[extract from : messing-around-with-azure-functions.linq](messing-around-with-azure-functions.linq)**

```csharp
	// quick warm up to assess a reasonable request per second to sustain.
	await RunTest(10, 1);
	await RunTest(80, 2);
	await RunTest(40, 4);
	await RunTest(20, 8);
	await RunTest(40, 8);
	await RunTest(80, 8);
	
	// a bit of monkey'ng around seems to indicate we shouldn't do more than 8 threads 
	// now quite a heavy test to see if we can force it to break?
	
	//await RunTest(2000, 8);
	
}

public static async Task RunTest(int cnt, int threads) {
	_client = new HttpClient(new HttpClientHandler { MaxConnectionsPerServer = threads });
	Console.Write($"{cnt,5} items, {threads,5} threads : ");
	var nums = await DoIt(cnt);
	checkGapsAndDuplicates(nums);
}

static async Task<int[]> DoIt(int cnt)
{
	var sw = new Stopwatch();
	sw.Start();	
	var results = await GetNumberWebClient(cnt);
	sw.Stop();
	double elapsedSeconds = sw.Elapsed.TotalSeconds;
	double rps = ((double)_totalRequests) / elapsedSeconds;
	Console.WriteLine($"independant count check, total [{_totalRequests, 5}] requests in [{elapsedSeconds:0.000}] seconds. [{rps,8:0.0}]rps");
	return results;
}

public static async Task<int[]> GetNumberWebClient(int times)
{
	var requests = Enumerable		
		.Range(0, times)
		.Select(e => _client.GetStringAsync(_uri));
	
	var nums = (await Task.WhenAll(requests)).Select(snum => _rex.MatchInt(snum)).ToArray();
	System.Threading.Interlocked.Add(ref _totalRequests, nums.Length);
	return nums;
}

public static (int[] gaps, Dups[] duplicates) checkGapsAndDuplicates(IEnumerable<int> src)
{
	var ordered = src.OrderBy(o => o).ToArray();
	var first = ordered[0];
	var last = ordered[ordered.Length - 1];

	int cnt = ordered.Length;
	var seq = Enumerable.Range(first, cnt).Select(i => i);
	var gaps = seq.Except(ordered).ToArray();
	var duplicates = ordered.GroupBy(o => o).Where(o => o.Count() > 1).Select(o => new Dups(o.Key, o.Count())).ToArray();
	return (gaps, duplicates);
}

```

Full linqpad scripts below;

* [test accessing Azure Function in parallel](messing-around-with-azure-functions.linq)
* [test how long azure function 'state' remains active](test-azure-function-timeout.linq)



