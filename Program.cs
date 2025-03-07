using ChannelBotsRemover;
using Microsoft.Extensions.Configuration;
using TL;

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

var config = configBuilder.Build().Get<AppConfiguration>();

if (config == null)
{
    Console.WriteLine($"[{DateTime.Now}] Failed to load appsettings.json");
    return -1;
}

WTelegram.Helpers.Log = (level, text) =>
{
    if(level > 2)
        Console.WriteLine($"[{DateTime.Now}][{level}] {text}");
};

var client = new WTelegram.Client(name =>
    name switch
    {
        "api_id" => config.ApiId.ToString(),
        "bot_token" => config.BotToken,
        "api_hash" => config.ApiHash,
        _ => null,
    }
);

await client.LoginBotIfNeeded();

var banRights = new ChatBannedRights()
{
    flags = ChatBannedRights.Flags.view_messages
};

var channel = (await client.Contacts_ResolveUsername(config.ChannelName)!)!.Channel;
if (channel == null)
{
    Console.WriteLine($"[{DateTime.Now}] Failed to resolve username");
    return -1;
}

Console.WriteLine($"[{DateTime.Now}] Getting participants");
var batch = await client.Channels_GetParticipants(
    new InputChannel(channel.ID, channel.access_hash),
    new ChannelParticipantsRecent());

if (batch is null)
{
    Console.WriteLine($"[{DateTime.Now}] No participants");
    return -1;
}

var didntDelete = true;
while (batch.count > 100 || !didntDelete)
{
    didntDelete = true;
    Console.WriteLine($"[{DateTime.Now}] Got {batch.count} recent participants");
    Console.WriteLine($"[{DateTime.Now}] {batch.participants.Count(x => x is ChannelParticipant)} are true");
    if(batch.participants.LastOrDefault(x => x is ChannelParticipant) is ChannelParticipant last)
        Console.WriteLine($"[{DateTime.Now}] Last one is {batch.users[last.UserId]} that joined on {last.date}");
    foreach (var participant in batch.participants
                 .Where(x => x is ChannelParticipant cp
                             && cp.date > config.IntervalStartUtc
                             && cp.date < config.IntervalEndUtc).Cast<ChannelParticipant>())
    {
        Console.WriteLine($"[{DateTime.Now}] Banning {batch.users[participant.UserId]} (id={participant.UserId}) that joined on {participant.date} (UTC)");

        var success = false;

        while (!success)
        {
            didntDelete = false;
            try
            {
                // ban the spam bot
                await client.Channels_EditBanned(channel, batch.users[participant.UserId], banRights);
                // unban it so it doesn't clog removed users list
                await client.Channels_EditBanned(channel, batch.users[participant.UserId], new ChatBannedRights());
                success = true;
            }
            catch (RpcException e)
            {
                Console.WriteLine($"[{DateTime.Now}] RPC Error: {e.Code} X={e.X} {e.Message}");
                success = false;
                // wait if telegram starts throttling us
                if (e.Code == 420)
                    await Task.Delay(e.X * 1000 + 50);
            }
        }
        
        await Task.Delay(25);
    }

    batch = await client.Channels_GetParticipants(new InputChannel(channel.ID, channel.access_hash),
        new ChannelParticipantsRecent());
    await Task.Delay(1000);
}

Console.WriteLine($"[{DateTime.Now}] DONE");
return 0;