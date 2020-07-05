<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net.Http</Namespace>
</Query>

#load "shared.linq"

static HttpClient client = new HttpClient();
static Stopwatch sw = new Stopwatch();

async Task Main()
{
	// lets run 5 times
	for(int i = 0; i<5; i++) await Run();
	
}
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