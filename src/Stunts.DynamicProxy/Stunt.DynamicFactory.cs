namespace Stunts
{
    partial class Stunt
    {
        static Stunt()
        {
            StuntFactory.Default = new DynamicStuntFactory();
            OnInitialized();
        }

        /// <summary>
        /// Invoked after the default <see cref="StuntFactory.Default"/> 
        /// is initialized.
        /// </summary>
        static partial void OnInitialized();
    }
}
