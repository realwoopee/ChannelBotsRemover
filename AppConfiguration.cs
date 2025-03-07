namespace ChannelBotsRemover;

public class AppConfiguration
{
    public string BotToken { get; set; }
    public long ApiId { get; set; }
    public string ApiHash { get; set; }
    public string ChannelName { get; set; }
    public DateTime IntervalStartUtc { get; set; }
    public DateTime IntervalEndUtc { get; set; }
}