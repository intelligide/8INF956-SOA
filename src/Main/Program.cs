using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UserSDK;
using BillSDK;
using StockSDK;

namespace Main
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Username: ");
            string username = Console.ReadLine();

            User user = User.GetUser(username);

            if(user == null)
            {
                Console.Error.Write("User doesn't exists.");
                return;
            }

            List<ItemLine> items = new List<ItemLine>();

            bool stop = false;
            do
            {
                string itemName = null;
                do
                {
                    Console.Write("Item: ");
                    itemName = Console.ReadLine().Trim();

                    if(itemName.Length > 0)
                    {
                        Console.Write("Quantity: ");
                        var q = UInt32.Parse(Console.ReadLine().Trim());

                        var item = StockManager.ReserveItem(q, itemName);

                        Console.Write("Confirm? [Y/n] ");
                        var r = Console.ReadLine().Trim();
                        if (r.ToLower() == "n" || r.ToLower() == "no")
                        {
                            StockManager.ReleaseItem(item);
                        }
                        else
                        {
                            items.Add(item);
                        }
                    }
                }
                while(itemName.Length > 0);

                Console.Write("Stop? [Y/n] ");
                string resp = Console.ReadLine().Trim();
                stop = resp.Length == 0 || resp.ToLower() == "y" || resp.ToLower() == "yes";
            }
            while(!stop);

            var bill = Bill.CreateBill(user, items);

            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("Item Count: " + bill.Lines.Count);
            foreach (var line in bill.Lines)
            {
                Console.WriteLine(line.Item.Name + " x" + line.Quantity + "      = " + line.SubTotal);
            }
            Console.WriteLine("TotalExclTax: " + bill.TotalExclTax);
            Console.WriteLine("Total: " + bill.Total);
        }   
    }
}
