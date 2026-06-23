namespace Ams2ChEd.Business.AMS2.Helpers
{
    public static class CopyFieldsToChildClassExtension
    {
        public static TDerived ConvertToChild<TBase, TDerived>(this TBase source)
            where TDerived : TBase, new()
        {
            var derived = new TDerived();
            foreach (var prop in typeof(TBase).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    prop.SetValue(derived, prop.GetValue(source));
                }
            }
            return derived;
        }
    }
}
