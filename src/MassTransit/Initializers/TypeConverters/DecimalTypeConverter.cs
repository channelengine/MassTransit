﻿namespace MassTransit.Initializers.TypeConverters
{
    using System;
    using System.Globalization;


    public class DecimalTypeConverter :
        ITypeConverter<string, decimal>,
        ITypeConverter<decimal, string>,
        ITypeConverter<decimal, sbyte>,
        ITypeConverter<decimal, byte>,
        ITypeConverter<decimal, short>,
        ITypeConverter<decimal, ushort>,
        ITypeConverter<decimal, int>,
        ITypeConverter<decimal, uint>,
        ITypeConverter<decimal, long>,
        ITypeConverter<decimal, ulong>
    {
        public bool TryConvert(string input, out decimal result)
        {
            return decimal.TryParse(input, out result);
        }

        public bool TryConvert(sbyte input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(byte input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(short input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(ushort input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(int input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(uint input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(long input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(ulong input, out decimal result)
        {
            result = Convert.ToDecimal(input);
            return true;
        }

        public bool TryConvert(decimal input, out string result)
        {
            result = input.ToString(CultureInfo.InvariantCulture);
            return true;
        }
    }
}
