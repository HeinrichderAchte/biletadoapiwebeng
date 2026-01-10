namespace Biletado.Models;

public class Room
{
    public Guid roomId { get; set; }
    public string roomName;
    public Guid storeyId;
    public DateTime deletedAt; 
}