using System;
using System.Management.Automation.Host;

namespace Inedo.Extensions.Windows.PowerShell
{
    // this needs to be faked in order to get Write-Host to work on PS 4.0
    internal sealed class InedoPSHostRawUserInterface : PSHostRawUserInterface
    {
        public override ConsoleColor BackgroundColor { get; set; }
        public override Size BufferSize { get; set; }
        public override Coordinates CursorPosition { get; set; }
        public override int CursorSize { get; set; }
        public override ConsoleColor ForegroundColor { get; set; }
        public override bool KeyAvailable => false;
        public override Size MaxPhysicalWindowSize => new Size(1000, 1000);
        public override Size MaxWindowSize => new Size(1000, 1000);
        public override Coordinates WindowPosition { get; set; }
        public override Size WindowSize { get; set; } = new Size(100, 100);
        public override string WindowTitle { get; set; }
        public override void FlushInputBuffer()
        {
        }
        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[0, 0];
        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            throw new NotImplementedException();
        }
        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
            throw new NotImplementedException();
        }
        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException();
        }
        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException();
        }
    }
}
