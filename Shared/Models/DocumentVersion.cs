namespace Shared.Models;

public class DocumentVersion
{
    public int Id { get; set; }
    public string Filename { get; set; }
    public string Author { get; set; }
    public int Version { get; set; }
    public DateTime UploadTimestamp { get; set; }
}