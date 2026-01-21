namespace Labyrinth.Sys
{
    public class BasicEnumRandomizer<TEnum> : IEnumRandomizer<TEnum> where TEnum : struct, Enum
    {
        private readonly TEnum[] _values = Enum.GetValues<TEnum>();
        private readonly Random _random = new ();

        public TEnum Next() => 
            _values[_random.Next(0, _values.Length)];
    }
}
