<Query Kind="Program">
  <NuGetReference>morelinq</NuGetReference>
  <Namespace>MoreLinq</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

#load "shared.linq"

static int _totalRequests = 0;
static HttpClient _client;
static Uri _uri = new Uri(azureFunctionUrl);
static Regex _rex = new System.Text.RegularExpressions.Regex(@"\[(.*)?\]", RegexOptions.Compiled);
static int _printEvery = 500;
static int _last = 0;
static int _printCnt = 0;
static bool _printEveryOn = false;
private static Stopwatch _everySW = new Stopwatch();

static async Task Main(string[] args)
{
	// get secrets
	
	
	SelfTest(); 	

	// quick warm up to assess a reasonable request per second to sustain.
	//await RunTest(10, 1);
	//await RunTest(80, 2);
	//await RunTest(40, 4);
	//await RunTest(20, 8);
	//await RunTest(40, 8);
	//await RunTest(80, 8);
	//await RunTest(80, 10);
	//await RunTest(80, 12);
	//await RunTest(100, 14);
	//await RunTest(120, 16);
	//await RunTest(200, 20);
	//await RunTest(400, 25);
	//await RunTest(600, 26);
	//await RunTest(700, 27);
	//await RunTest(800, 28);
	//await RunTest(900, 29);
	//await RunTest(1000, 30);
	//await RunTest(400, 30);
	//await RunTest(600, 40);
	//await RunTest(700, 50);
	//await RunTest(800, 60);
	//await RunTest(900, 70);

	_printEveryOn = true;
	await RunTest(20000, 30);
	
}

public static async Task RunTest(int cnt, int threads) {
	_everySW.Reset();
	_everySW.Start();
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
	double rps = ((double)results.Length) / elapsedSeconds;
	Console.WriteLine($"independant count check, total [{_totalRequests, 5}] requests in [{elapsedSeconds:0.000}] seconds. [{rps,8:0.0}]rps");
	return results;
}

public static async Task<int[]> GetNumberWebClient(int times)
{
	var requests = Enumerable
		.Range(0, times)
		.Select(e =>
		{
			var queuedDownload = _client.GetStringAsync(_uri);
			queuedDownload.ContinueWith(_ => {
				int total = System.Threading.Interlocked.Increment(ref _totalRequests);
				if (total % _printEvery == 0)
				{
					var seconds = _everySW.Elapsed.TotalSeconds;
					var rps = (total - _last) / seconds;
					_everySW.Restart();
					Console.Write($"{total,-5} [{rps,-3:0}] ");
					_last = total;
					var printCnt = System.Threading.Interlocked.Increment(ref _printCnt);
					if(printCnt % 20==0) Console.WriteLine();
				}
			});
			return queuedDownload;
		});
	
	
	var nums = (await Task.WhenAll(requests)).Select(snum => _rex.MatchInt(snum)).ToArray();
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

public static class Extensions { 
	public static int MatchInt(this Regex rex, string text) {
		return int.Parse(rex.Match(text).Groups[1].Value);
	}
}

