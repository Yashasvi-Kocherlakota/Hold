using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;




public static class HolidayRisk
{
    [FunctionName("HolidayRisk")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

        string origin = data.GetValueOrDefault("origin");
        string destination = data.GetValueOrDefault("destination");
        string dateStr = data.GetValueOrDefault("date");

        if (!DateTime.TryParse(dateStr, out var date))
            return new BadRequestObjectResult("Invalid date format");

        var client = new HttpClient();
        var year = date.Year;

        async Task<List<DateTime>> GetHolidays(string country)
        {
            var url = $"https://date.nager.at/api/v3/publicholidays/{year}/{country}";
            var response = await client.GetStringAsync(url);
            var holidays = JArray.Parse(response);
            return holidays.Select(j => DateTime.Parse(j["date"]!.ToString())).ToList();
        }

        var originHolidays = await GetHolidays(origin);
        var destHolidays = await GetHolidays(destination);

        int risk = 30;
        if (originHolidays.Contains(date) || destHolidays.Contains(date))
            risk = 100;
        else if (originHolidays.Any(d => Math.Abs((d - date).Days) <= 1) ||
                 destHolidays.Any(d => Math.Abs((d - date).Days) <= 1))
            risk = 70;

        return new OkObjectResult(new { riskScore = risk });
    }
}
