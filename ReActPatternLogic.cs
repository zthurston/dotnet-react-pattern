using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Extensions;
using OpenAI.Chat;

namespace Dotnet.ReActPattern;
public class ChatBot
{
    private readonly ChatClient chatClient;
    private readonly ILogger<ChatBot> logger;
    private readonly List<ChatMessage> messages = new();
    private readonly string prompt;
    public ChatBot(ChatClient chatClient, ILogger<ChatBot> logger, string prompt)
    {
        this.chatClient = chatClient;
        this.logger = logger;
        this.prompt = prompt;//.Replace("\n", "");
        messages.Add(ChatMessage.CreateSystemMessage(ChatMessageContentPart.CreateTextPart(this.prompt)));
    }


    public async Task<ChatMessageContentPart> ExecuteAsync(string message)
    {
        var userChat = ChatMessage.CreateUserMessage(ChatMessageContentPart.CreateTextPart(message));
        messages.Add(userChat);
        var completion = await chatClient.CompleteChatAsync(messages);
        Console.WriteLine($"input token count: {completion.Value.Usage.InputTokenCount} details: {completion.Value.Usage.InputTokenDetails.CachedTokenCount}");
        Console.WriteLine($"output token count: {completion.Value.Usage.OutputTokenCount} details: {completion.Value.Usage.OutputTokenDetails.ReasoningTokenCount}");

        var firstContent = completion.Value.Content.First();
        Console.WriteLine(firstContent.Text);
        messages.Add(ChatMessage.CreateAssistantMessage(firstContent));
        return firstContent;
    }

    public List<string?> NonSystemMessages()
    {
        return messages
            .Where(m => m is not SystemChatMessage)
            .Select(m => m.Content.FirstOrDefault()?.Text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}

public static partial class Regexes
{
    [GeneratedRegex(@"^Action\: (\w+)\: (.*)$")]
    public static partial Regex ActionRegex();
        [GeneratedRegex(@"^Answer\: (\w+)\: (.*)$")]
    public static partial Regex AnswerRegex();
}

public class Logic(HttpClient httpClient, ChatBot chatBot, ILogger<Logic> logger)
{
    public async Task<List<string?>> Query(string question)
    {
        const int maxInterations = 5;
        string nextPrompt = question;
        for (int i = 0; i < maxInterations; i++)
        {
            logger.LogInformation("running iteration: {i}", i + 1);
            var content = await chatBot.ExecuteAsync(nextPrompt);

            // TODO: need to exit when the llm is done

            var splitcontent = content.Text.Split('\n');
            foreach (var substring in splitcontent.Where(sc => !string.IsNullOrWhiteSpace(sc)))
            {
                logger.LogInformation("looking at substring: {sub}", substring);
                var match = Regexes.ActionRegex().Match(substring);
                if (match.Success)
                {
                    string actionType = match.Groups[1].Value;
                    logger.LogInformation("action type: {actionType}", actionType);
                    string actionDetail = match.Groups[2].Value;
                    logger.LogInformation("action detail: {actionDetail}", actionDetail);
                    nextPrompt = await GetNextPromptAsync(actionType, actionDetail);
                    break;
                }
                else
                {
                    logger.LogInformation("no Action match for substring: {sub}", substring.Length > 5 ? substring[..5] : substring);
                }
            }
            /*
                while i < max_turns:
                    actions = [action_re.match(a) for a in result.split('\n') if action_re.match(a)]
                    if actions:
                        # There is an action to run
                        action, action_input = actions[0].groups()
                        if action not in known_actions:
                            raise Exception("Unknown action: {}: {}".format(action, action_input))
                        print(" -- running {} {}".format(action, action_input))
                        observation = known_actions[action](action_input)
                        print("Observation:", observation)
                        next_prompt = "Observation: {}".format(observation)
                    else:
                        return
            */
        }
        return chatBot.NonSystemMessages();
    }

    private async Task<string> GetNextPromptAsync(string actionType, string actionDetail)
    {
        switch (actionType.ToLower())
        {
            case "wikipedia":
                var observation = await QueryWikipediaAsync(actionDetail);
                return $"Observation: {observation}";
            default:
                logger.LogWarning("unknown actionType: {at}", actionType);
                return "";
                //throw new Exception("Unknown action type :(");
        }
    }

    private async Task<string> QueryWikipediaAsync(string q)
    {
        var queryBuilder = new QueryBuilder
        {
            { "action", "query" },
            { "list", "search" },
            { "srsearch", q },
            { "format", "json" }
        };
        var queryString = queryBuilder.ToString();
        string uri = "https://en.wikipedia.org/w/api.php" + queryString;
        logger.LogInformation("querying wikipedia with uri: {uri}", uri);
        var response = await httpClient.GetFromJsonAsync<WikiResponse>(uri);
        //var content = await response.Content.ReadAsStringAsync();
        // logger.LogInformation("wikipedia response: {w}", content);

        var allSnippets = response?.query?.search?.Select(s => s.snippet);
        var joined = allSnippets != null ? string.Join(Environment.NewLine, allSnippets):string.Empty;
        logger.LogInformation("all snippets from wikipedia: {allSnippets}", joined);
        return joined;
        //   def wikipedia(q):
        // return httpx.get("https://en.wikipedia.org/w/api.php", params={
        //     "action": "query",
        //     "list": "search",
        //     "srsearch": q,
        //     "format": "json"
        // }).json()["query"]["search"][0]["snippet"]
    }
}
public class WikiResponse
{
    public Query? query { get; init; }
}
public class Query
{
    public List<Search>? search { get; init; }
}
public class Search
{
    public string? snippet { get; init; }
}
/*
class ChatBot:
    def __init__(self, system=""):
        self.system = system
        self.messages = []
        if self.system:
            self.messages.append({"role": "system", "content": system})
    
    def __call__(self, message):
        self.messages.append({"role": "user", "content": message})
        result = self.execute()
        self.messages.append({"role": "assistant", "content": result})
        return result
    
    def execute(self):
        completion = openai.ChatCompletion.create(model="gpt-3.5-turbo", messages=self.messages)
        # Uncomment this to print out token usage each time, e.g.
        # {"completion_tokens": 86, "prompt_tokens": 26, "total_tokens": 112}
        # print(completion.usage)
        return completion.choices[0].message.content
*/
/*
action_re = re.compile('^Action: (\w+): (.*)$')

def query(question, max_turns=5):
    i = 0
    bot = ChatBot(prompt)
    next_prompt = question
    while i < max_turns:
        i += 1
        result = bot(next_prompt)
        print(result)
        actions = [action_re.match(a) for a in result.split('\n') if action_re.match(a)]
        if actions:
            # There is an action to run
            action, action_input = actions[0].groups()
            if action not in known_actions:
                raise Exception("Unknown action: {}: {}".format(action, action_input))
            print(" -- running {} {}".format(action, action_input))
            observation = known_actions[action](action_input)
            print("Observation:", observation)
            next_prompt = "Observation: {}".format(observation)
        else:
            return


def simon_blog_search(q):
    results = httpx.get("https://datasette.simonwillison.net/simonwillisonblog.json", params={
        "sql": """
        select
          blog_entry.title || ': ' || substr(html_strip_tags(blog_entry.body), 0, 1000) as text,
          blog_entry.created
        from
          blog_entry join blog_entry_fts on blog_entry.rowid = blog_entry_fts.rowid
        where
          blog_entry_fts match escape_fts(:q)
        order by
          blog_entry_fts.rank
        limit
          1""".strip(),
        "_shape": "array",
        "q": q,
    }).json()
    return results[0]["text"]

def calculate(what):
    return eval(what)

known_actions = {
    "wikipedia": wikipedia,
    "calculate": calculate,
    "simon_blog_search": simon_blog_search
}
*/

