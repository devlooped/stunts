namespace Sample
{
    public interface ICalculatorMemory
    {
        void Add(int value);

        void Subtract(int value);

        void Clear();

        int? Recall();
    }
}
