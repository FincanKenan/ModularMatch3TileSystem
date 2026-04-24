namespace ZenMatch.Runtime
{
    public static class BoardGenerationMath
    {
        public static int RoundUpToMultipleOfThree(int value)
        {
            if (value <= 0)
                return 0;

            int remainder = value % 3;
            if (remainder == 0)
                return value;

            return value + (3 - remainder);
        }

        public static int RoundDownToMultipleOfThree(int value)
        {
            if (value <= 0)
                return 0;

            return value - (value % 3);
        }
    }
}