<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net</Namespace>
</Query>


public static string azureFunctionUrl = "https://REDACTED_AZUREFUNCTION_URL.azurewebsites.net/api/Test1?name=fred&code=REDACTED_API_FUNCTION_KEY"
	.Replace("REDACTED_AZUREFUNCTION_URL", Util.GetPassword("azfunction-spike-url"))
	.Replace("REDACTED_API_FUNCTION_KEY", Util.GetPassword("azfunction-spike-key"));