
using Heimdall.Example.Raw.Features.Notes;

namespace Heimdall.Example.Raw.Utilities.BackgroundServices
{
    public class DummyNoteLoader:BackgroundService
    {
        private readonly NoteService _noteService;
        public DummyNoteLoader(NoteService noteService)
        {
            _noteService = noteService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                foreach (var num in Enumerable.Range(1,1000))
                {
                    await _noteService.CreateAsync($"Note {num}",$"I'm dummy note number {num}."); 
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Notes did not load");
            }
        }
    }
}
