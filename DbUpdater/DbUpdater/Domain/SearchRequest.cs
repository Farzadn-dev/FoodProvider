using System.ComponentModel.DataAnnotations;

namespace DbUpdater.Domain
{
    public class SearchRequest
    {
        [Key]
        public required Guid Id { get; set; }

        public required string[] Tags { get; set; }

        public string? FilePath { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        public DateTime? FinishDate { get; set; }

        public RequestStatus Status { get; set; }
    }

    public enum RequestStatus
    {
        Pending,
        Completed,
        Failed
    }
}
