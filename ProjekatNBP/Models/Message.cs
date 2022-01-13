namespace ProjekatNBP.Models
{
    public record Message(string From, string Text, long Timestamp, bool Read = false);
}
