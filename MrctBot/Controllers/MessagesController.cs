using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Chronic;
using Microsoft.Bot.Connector;
using Microsoft.ProjectOxford.Linguistics;
using Microsoft.ProjectOxford.Linguistics.Contract;

namespace MrctBot.Controllers
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static readonly Dictionary<string, List<string>> Words;

        static MessagesController()
        {
            Words = GetMatches(
                File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Content\stree.txt")))
                .Cast<Match>()
                .GroupBy(match => match.Groups[2].ToString())
                .ToDictionary(grouping => grouping.Key,
                    grouping => grouping.Select(match => match.Groups[3].ToString().ToLower()).Distinct().ToList());
        }

        private static async Task<string> GetTree(string text)
        {
            var client = new LinguisticsClient("put your own id here");
            var analyzers = await client.ListAnalyzersAsync();
            var linguisticsAnalyzer = analyzers.First(analyzer => analyzer.Kind == "Constituency_Tree");

            var result = await client.AnalyzeTextAsync(new AnalyzeTextRequest
            {
                Language = "en",
                Text = text,
                AnalyzerIds = new[] {linguisticsAnalyzer.Id}
            });
            return result[0].Result.ToString();
        }

        public async Task<HttpResponseMessage> Post([FromBody] Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                var answerText = activity.Text;

                GetMatches(await GetTree(activity.Text)).Cast<Match>().ForEach(match =>
                {
                    var key = match.Groups[2].ToString();

                    if (!Words.ContainsKey(key)) return;

                    var original = match.Groups[3].ToString().ToLower();
                    var values = Words[key].Where(value => value != original).ToArray();

                    if (!values.Any()) return;

                    var regex = new Regex($"(\\b{original}\\b)");
                    answerText = regex.Replace(answerText,
                        values[new Random(Guid.NewGuid().GetHashCode()).Next(values.Length)], 1);
                });

                await
                    new ConnectorClient(new Uri(activity.ServiceUrl)).Conversations.ReplyToActivityAsync(
                        activity.CreateReply(answerText));
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private static MatchCollection GetMatches(string stree)
        {
            return Regex.Matches(stree, @"(\((\w+)\s([\w’]+)\))", RegexOptions.Multiline);
        }
    }
}