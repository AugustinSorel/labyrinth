namespace Labyrinth.Sys
{
    /// <summary>
    /// Random generator for enum types.
    /// </summary>
    /// <typeparam name="TEnum">Enum type</typeparam>
    public interface IEnumRandomizer<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// Generates a random value within an enum. 
        /// </summary>
        /// <returns>A random value of the enum</returns>
        TEnum Next();
    }
}
