namespace Deremis.Engine.Systems.Components
{
    public struct Render
    {
        public bool Screen;
        public bool Shadows;

        public Render(bool screen, bool shadows)
        {
            Screen = screen;
            Shadows = shadows;
        }
    }
}