namespace ProjekatNBP.Models
{
    public record Message(int From, string Username, string Text, long Timestamp, bool Read = false);
}
