using System;
using StockSDK;

namespace BillSDK
{
    public class BillLine
    {
        public Item Item { get; set; }

        public uint Quantity { get; set; }

        public float SubTotal { get; set; }
    }
}