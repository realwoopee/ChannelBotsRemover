using ChannelBotsRemover;
using Microsoft.Extensions.Configuration;
using TL;

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

var config = configBuilder.Build().Get<AppConfiguration>();

if (config == null)
{
    Console.WriteLine("Failed to load appsettings.json");
    return -1;
}

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

Console.WriteLine("Getting participants");
var current = await client.Channels_GetParticipants(
    new InputChannel(channel.ID, channel.access_hash),
    new ChannelParticipantsRecent());

if (current is null)
{
    Console.WriteLine("No participants");
    return -1;
}

var didntDelete = true;
while (current.count > 100 || !didntDelete)
{
    didntDelete = true;
    Console.WriteLine($"Got {current.count} recent participants");
    foreach (var participant in current.participants
                 .Where(x => x is ChannelParticipant cp
                             && cp.date > config.IntervalStartUtc
                             && cp.date < config.IntervalEndUtc).Cast<ChannelParticipant>())
    {
        Console.WriteLine($"Banning user_id={participant.user_id} that joined on {participant.date} (UTC)");

        var success = false;

        while (!success)
        {
            didntDelete = false;
            try
            {
                // ban the spam bot
                await client.Channels_EditBanned(channel, current.users[participant.UserId], banRights);
                await Task.Delay(500);
                // unban it so it doesn't clog removed users list
                await client.Channels_EditBanned(channel, current.users[participant.UserId], new ChatBannedRights());
                success = true;
            }
            catch (RpcException e)
            {
                success = false;
                // wait if telegram starts throttling us
                if (e.Code == 420)
                    await Task.Delay(e.X * 1000 + 50);
            }
        }
        
        await Task.Delay(500);
    }

    current = await client.Channels_GetParticipants(new InputChannel(channel.ID, channel.access_hash),
        new ChannelParticipantsRecent());
    await Task.Delay(1000);
}

Console.WriteLine("DONE");
return 0;