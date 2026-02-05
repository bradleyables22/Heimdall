using System.ComponentModel.DataAnnotations;

namespace Heimdall.Example.Raw.Features.Notes
{
    public class CreateNoteRequest
    {
        [StringLength(100,MinimumLength =5)]
        public string Title { get; set; } = string.Empty;
        [StringLength(200, MinimumLength = 5)]
        public string Content { get; set; } = string.Empty;
    }
}
