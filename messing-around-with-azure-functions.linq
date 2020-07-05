<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net</Namespace>
</Query>

#load "shared.linq"

static int _totalRequests = 0;


static async Task Main(string[] args)
{
	// get secrets
	
	
	SelfTest(); 	
	await RunTest(30, 2);
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


static async Task<int[][]> Run(int numItems, int maxThreads)
{
	var tasks = new List<Task<int[]>>();
	for (int i = 0; i < maxThreads; i++)
	{
		tasks.Add(Task.Run(() => GetNumberWebClient(numItems)));
	}
	return await Task.WhenAll(tasks);
}		

public static int[] GetNumberWebClient(int times)
{
	var nums = new int[times];
	int i = 0;
	
	// need to read this value from configuration that's not checked in with the linqpad script...
	var uri = new Uri(azureFunctionUrl);
	using(var wc = new WebClient())
	{
		for(int x = 0 ; x< times; x++)
		{
			var response = wc.DownloadString(uri);
			var re = new System.Text.RegularExpressions.Regex(@"\[(.*)?\]").Match(response).Groups[1].Value;
			System.Threading.Interlocked.Increment(ref _totalRequests);
			int num = int.Parse(re);
			nums[i++] = num;
		}
	}
	return nums;
}

public struct Dups {
	public Dups(int num, int cnt) { Num = num; Cnt = cnt; }
	public int Num; 
	public int Cnt;
}

public static void EnsureSame(params (string testName, object lhs, object rhs)[] compares)
{
	bool errors = false;
	var diffs = new List<(Util.IDifResult diff, string test)>();
	foreach (var compare in compares)
	{
		var diff = Util.Dif(compare.lhs, compare.rhs);
		if (!diff.IsSame)
		{
			errors = true;
			diffs.Add((diff, compare.testName));
		}
	}
	if (errors)
	{
		foreach (var d in diffs) d.Dump(d.test);
		throw new ApplicationException("system not stable, halting!");
	}
}

public static void SelfTest()
{
	var ns = new[] { 10, 11, 13, 14, 14, 15, 16, 19, 20 };
	(var gaps, var dups) = checkGapsAndDuplicates(ns);
	EnsureSame(
		("gaps", gaps, new[] { 12, 17, 18 }),
		("duplicates", dups, new[] { new Dups(14, 2) })
	);
}