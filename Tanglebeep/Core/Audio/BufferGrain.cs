namespace Tanglebeep.Audio {
    /// <summary>
    /// A grain backed by a slice of an existing sample array — a noise pool slice now, a decoded
    /// audio asset later. The slice is (array, offset, count), the boring zero-dependency form: a
    /// <c>Span</c> is a ref struct and can't be a field, and <c>Memory</c> would pull a net472
    /// package, so plain offset/count it is. Because the array is read-only data, this is a pure
    /// <see cref="Grain.Evaluate"/> — no filter state lives here even though the data was filtered.
    ///
    /// <para>The grain carries the sample rate the data was generated at and linearly interpolates
    /// on read, so it is correct whether the data matches the render rate (interpolation is a no-op)
    /// or not (a real asset at a different rate). Interpolation never reads past the slice, so one
    /// slice can't bleed into the next — which is what keeps left/right slices decorrelated.</para>
    /// </summary>
    public sealed class BufferGrain : Grain {
        public float[] Data { get; }
        public int Offset { get; }
        public int Count { get; }
        public int SampleRate { get; }

        public BufferGrain(float[] data, int offset, int count, int sampleRate) {
            Data = data;
            Offset = offset;
            Count = count;
            SampleRate = sampleRate;
        }

        public override double Duration => (double)Count / SampleRate;

        public override float Evaluate(double t) {
            if (t < 0.0) {
                return 0f;
            }

            double srcPos = t * SampleRate;
            int i = (int)srcPos;
            if (i >= Count) {
                return 0f;
            }

            float a = Data[Offset + i];
            if (i + 1 >= Count) {
                return a; // last sample of the slice: nothing to interpolate toward
            }

            float b = Data[Offset + i + 1];
            return (float)(a + (b - a) * (srcPos - i));
        }
    }
}
