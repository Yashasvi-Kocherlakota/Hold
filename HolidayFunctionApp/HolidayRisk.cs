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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        // Try to get inputs from the URL query parameters
        string origin = req.Query["origin"];
        string destination = req.Query["destination"];
        string dateStr = req.Query["date"];

        // If any are missing, try to read them from the request body (for POST)
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination) || string.IsNullOrEmpty(dateStr))
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            origin = origin ?? data?.GetValueOrDefault("origin");
            destination = destination ?? data?.GetValueOrDefault("destination");
            dateStr = dateStr ?? data?.GetValueOrDefault("date");
        }

        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination) || string.IsNullOrEmpty(dateStr))
        {
            return new BadRequestObjectResult("Missing origin, destination, or date.");
        }

        if (!DateTime.TryParse(dateStr, out var date))
        {
            return new BadRequestObjectResult("Invalid date format. Please use YYYY-MM-DD.");
        }

        var client = new HttpClient();
        var year = date.Year;

        async Task<List<DateTime>> GetHolidays(string country)
        {
            var url = $"https://date.nager.at/api/v3/publicholidays/{year}/{country}";
            var response = await client.GetStringAsync(url);
            var holidays = JArray.Parse(response);
            return holidays.Select(j => DateTime.Parse(j["date"]!.ToString())).ToList();
        }

        try
        {
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
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to fetch holidays or calculate risk.");
            return new StatusCodeResult(500);
        }
    }
}
