﻿
namespace Questor.Modules.Actions
{
    using System;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.States;

    public class Sell
    {
        public StateSell State { get; set; }

        public int Item { get; set; }
        public int Unit { get; set; }

        private DateTime _lastAction;




        public void ProcessState()
        {
            DirectMarketWindow marketWindow = Cache.Instance.DirectEve.Windows.OfType<DirectMarketWindow>().FirstOrDefault();
            DirectContainer hangar = Cache.Instance.DirectEve.GetItemHangar();
            DirectMarketActionWindow sellWindow = Cache.Instance.DirectEve.Windows.OfType<DirectMarketActionWindow>().FirstOrDefault(w => w.IsSellAction);

            switch (State)
            {
                case StateSell.Idle:
                case StateSell.Done:
                    break;

                case StateSell.Begin:
                    State = StateSell.StartQuickSell;
                    break;

                case StateSell.StartQuickSell:

                    if (DateTime.Now.Subtract(_lastAction).TotalSeconds < 1)
                        break;
                    _lastAction = DateTime.Now;

                    if (hangar.Window == null)
                    {
                        // No, command it to open
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                        break;
                    }

                    if (!hangar.IsReady)
                        break;

                    DirectItem directItem = hangar.Items.FirstOrDefault(i => (i.TypeId == Item));
                    if (directItem == null)
                    {
                        Logging.Log("Sell: Item " + Item + " no longer exists in the hanger");
                        break;
                    }

                    // Update Quantity
                    if (Unit == 00)
                       Unit = directItem.Quantity;
                    

                       
                    Logging.Log("Sell: Starting QuickSell for " + Item);
                    if (!directItem.QuickSell())
                    {
                        _lastAction = DateTime.Now.AddSeconds(-5);

                        Logging.Log("Sell: QuickSell failed for " + Item + ", retrying in 5 seconds");
                        break;
                    }

                    State = StateSell.WaitForSellWindow;
                    break;

                case StateSell.WaitForSellWindow:


                    //if (sellWindow == null || !sellWindow.IsReady || sellWindow.Item.ItemId != Item)
                    //    break;

                    // Mark as new execution
                    _lastAction = DateTime.Now;

                    Logging.Log("Sell: Inspecting sell order for " + Item);
                    State = StateSell.InspectOrder;
                    break;

                case StateSell.InspectOrder:
                    // Let the order window stay open for 2 seconds
                    if (DateTime.Now.Subtract(_lastAction).TotalSeconds < 2)
                        break;

                    if (!sellWindow.OrderId.HasValue || !sellWindow.Price.HasValue || !sellWindow.RemainingVolume.HasValue)
                    {
                        Logging.Log("Sell: No order available for " + Item);

                        sellWindow.Cancel();
                        State = StateSell.WaitingToFinishQuickSell;
                        break;
                    }

                    double price = sellWindow.Price.Value;

                    Logging.Log("Sell: Selling " + Unit + " of " + Item + " [Sell price: " + (price * Unit).ToString("#,##0.00") + "]");
                   
                    sellWindow.Accept();


                    _lastAction = DateTime.Now;
                    State = StateSell.WaitingToFinishQuickSell;
                    break;

                case StateSell.WaitingToFinishQuickSell:
                    if (sellWindow == null || !sellWindow.IsReady || sellWindow.Item.ItemId != Item)
                    {
                        DirectWindow modal = Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.IsModal);
                        if (modal != null)
                            modal.Close();

                        State = StateSell.Done;
                        break;
                    }
                    break;

            }


        }



    }
}