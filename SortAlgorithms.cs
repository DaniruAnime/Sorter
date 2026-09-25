using System;
using System.Diagnostics;
using System.Threading;

namespace Sorter
{
    public enum SortAlgorithm
    {
        Bubble,
        Insertion,
        Shaker,
        Quick,
        Bogo
    }

    public sealed class SortResult
    {
        public SortAlgorithm Algorithm { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public long Iterations { get; private set; }
        public bool Completed { get; private set; }
        public bool Cancelled { get; private set; }
        public double[] SortedData { get; private set; }
        public string Message { get; private set; }

        public SortResult(SortAlgorithm algorithm, TimeSpan elapsed, long iterations,
            bool completed, bool cancelled, double[] sortedData, string message)
        {
            Algorithm = algorithm;
            Elapsed = elapsed;
            Iterations = iterations;
            Completed = completed;
            Cancelled = cancelled;
            SortedData = sortedData;
            Message = message;
        }
    }

    public static class SortAlgorithms
    {
        public static SortResult Run(SortAlgorithm algorithm, double[] source, bool ascending,
            Action<double[], int, int, string> onChanged, CancellationToken token)
        {
            double[] data = (double[])source.Clone();
            Stopwatch stopwatch = Stopwatch.StartNew();
            long iterations = 0;

            try
            {
                Action<int, int, string> publish = delegate(int firstIndex, int secondIndex, string operation)
                {
                    if (onChanged == null)
                        return;

                    // Время визуализации не должно входить в время алгоритма.
                    stopwatch.Stop();
                    try
                    {
                        onChanged(data, firstIndex, secondIndex, operation);
                    }
                    finally
                    {
                        stopwatch.Start();
                    }
                };

                publish(-1, -1, "Начальное состояние");

                switch (algorithm)
                {
                    case SortAlgorithm.Bubble:
                        BubbleSort(data, ascending, publish, token, ref iterations);
                        break;
                    case SortAlgorithm.Insertion:
                        InsertionSort(data, ascending, publish, token, ref iterations);
                        break;
                    case SortAlgorithm.Shaker:
                        ShakerSort(data, ascending, publish, token, ref iterations);
                        break;
                    case SortAlgorithm.Quick:
                        QuickSort(data, 0, data.Length - 1, ascending, publish, token, ref iterations);
                        publish(-1, -1, "Сортировка завершена");
                        break;
                    case SortAlgorithm.Bogo:
                        BogoSort(data, ascending, publish, token, ref iterations);
                        publish(-1, -1, "Сортировка завершена");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("algorithm", "Неизвестный алгоритм сортировки.");
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return new SortResult(
                    algorithm,
                    stopwatch.Elapsed,
                    iterations,
                    false,
                    true,
                    data,
                    "Остановлено пользователем");
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                return new SortResult(
                    algorithm,
                    stopwatch.Elapsed,
                    iterations,
                    false,
                    false,
                    data,
                    "Ошибка: " + exception.Message);
            }

            stopwatch.Stop();
            return new SortResult(
                algorithm,
                stopwatch.Elapsed,
                iterations,
                true,
                false,
                data,
                "Готово");
        }

        private static bool ShouldSwap(double firstValue, double secondValue, bool ascending)
        {
            return ascending ? firstValue > secondValue : firstValue < secondValue;
        }

        private static void Swap(double[] data, int firstIndex, int secondIndex,
            Action<int, int, string> publish)
        {
            double temporaryValue = data[firstIndex];
            data[firstIndex] = data[secondIndex];
            data[secondIndex] = temporaryValue;
            publish(firstIndex, secondIndex, "Обмен элементов");
        }

        private static void BubbleSort(double[] data, bool ascending,
            Action<int, int, string> publish, CancellationToken token, ref long iterations)
        {
            for (int passIndex = 0; passIndex < data.Length - 1; ++passIndex)
            {
                bool changed = false;
                for (int comparisonIndex = 0; comparisonIndex < data.Length - 1 - passIndex; ++comparisonIndex)
                {
                    token.ThrowIfCancellationRequested();
                    ++iterations;
                    if (ShouldSwap(data[comparisonIndex], data[comparisonIndex + 1], ascending))
                    {
                        Swap(data, comparisonIndex, comparisonIndex + 1, publish);
                        changed = true;
                    }
                }
                if (!changed)
                    break;
            }

            publish(-1, -1, "Сортировка завершена");
        }

        private static void InsertionSort(double[] data, bool ascending,
            Action<int, int, string> publish, CancellationToken token, ref long iterations)
        {
            for (int currentIndex = 1; currentIndex < data.Length; ++currentIndex)
            {
                token.ThrowIfCancellationRequested();
                ++iterations;
                double keyValue = data[currentIndex];
                int previousIndex = currentIndex - 1;

                while (previousIndex >= 0 && ShouldSwap(data[previousIndex], keyValue, ascending))
                {
                    token.ThrowIfCancellationRequested();
                    data[previousIndex + 1] = data[previousIndex];
                    publish(previousIndex, previousIndex + 1, "Сдвиг элемента");
                    previousIndex--;
                }

                data[previousIndex + 1] = keyValue;
                publish(previousIndex + 1, currentIndex, "Вставка элемента");
            }

            publish(-1, -1, "Сортировка завершена");
        }

        private static void ShakerSort(double[] data, bool ascending,
            Action<int, int, string> publish, CancellationToken token, ref long iterations)
        {
            int leftBoundary = 0;
            int rightBoundary = data.Length - 1;

            while (leftBoundary < rightBoundary)
            {
                bool changed = false;

                for (int forwardIndex = leftBoundary; forwardIndex < rightBoundary; ++forwardIndex)
                {
                    token.ThrowIfCancellationRequested();
                    ++iterations;
                    if (ShouldSwap(data[forwardIndex], data[forwardIndex + 1], ascending))
                    {
                        Swap(data, forwardIndex, forwardIndex + 1, publish);
                        changed = true;
                    }
                }

                rightBoundary--;
                if (!changed)
                    break;

                changed = false;
                for (int backwardIndex = rightBoundary; backwardIndex > leftBoundary; backwardIndex--)
                {
                    token.ThrowIfCancellationRequested();
                    ++iterations;
                    if (ShouldSwap(data[backwardIndex - 1], data[backwardIndex], ascending))
                    {
                        Swap(data, backwardIndex - 1, backwardIndex, publish);
                        changed = true;
                    }
                }

                ++leftBoundary;
                if (!changed)
                    break;
            }

            publish(-1, -1, "Сортировка завершена");
        }

        private static void QuickSort(double[] data, int lowIndex, int highIndex, bool ascending,
            Action<int, int, string> publish, CancellationToken token, ref long iterations)
        {
            if (lowIndex >= highIndex)
                return;

            token.ThrowIfCancellationRequested();
            int leftIndex = lowIndex;
            int rightIndex = highIndex;
            int pivotIndex = lowIndex + (highIndex - lowIndex) / 2;
            double pivotValue = data[pivotIndex];

            publish(pivotIndex, -1, "Выбран опорный элемент");

            while (leftIndex <= rightIndex)
            {
                token.ThrowIfCancellationRequested();
                ++iterations;

                while (ascending ? data[leftIndex] < pivotValue : data[leftIndex] > pivotValue)
                    ++leftIndex;

                while (ascending ? data[rightIndex] > pivotValue : data[rightIndex] < pivotValue)
                    rightIndex--;

                if (leftIndex <= rightIndex)
                {
                    if (leftIndex != rightIndex)
                        Swap(data, leftIndex, rightIndex, publish);
                    else
                        publish(leftIndex, rightIndex, "Элемент уже на месте");

                    ++leftIndex;
                    rightIndex--;
                }
            }

            if (lowIndex < rightIndex)
                QuickSort(data, lowIndex, rightIndex, ascending, publish, token, ref iterations);
            if (leftIndex < highIndex)
                QuickSort(data, leftIndex, highIndex, ascending, publish, token, ref iterations);
        }

        private static bool IsSorted(double[] data, bool ascending)
        {
            for (int currentIndex = 1; currentIndex < data.Length; ++currentIndex)
            {
                if (ascending ? data[currentIndex - 1] > data[currentIndex] : data[currentIndex - 1] < data[currentIndex])
                    return false;
            }

            return true;
        }

        private static void Shuffle(double[] data, Random random,
            Action<int, int, string> publish, CancellationToken token)
        {
            for (int currentIndex = data.Length - 1; currentIndex > 0; currentIndex--)
            {
                token.ThrowIfCancellationRequested();
                int randomIndex = random.Next(currentIndex + 1);

                if (currentIndex != randomIndex)
                {
                    double temporaryValue = data[currentIndex];
                    data[currentIndex] = data[randomIndex];
                    data[randomIndex] = temporaryValue;
                    publish(currentIndex, randomIndex, "Перемещение элемента");
                }
            }

            publish(-1, -1, "Случайное перемешивание");
        }

        private static void BogoSort(double[] data, bool ascending,
            Action<int, int, string> publish, CancellationToken token, ref long iterations)
        {
            if (IsSorted(data, ascending))
            {
                publish(-1, -1, "Сортировка завершена");
                return;
            }

            Random random = new Random(Environment.TickCount);

            while (!IsSorted(data, ascending))
            {
                token.ThrowIfCancellationRequested();
                ++iterations;
                Shuffle(data, random, publish, token);
            }

            publish(-1, -1, "Сортировка завершена");
        }
    }
}
