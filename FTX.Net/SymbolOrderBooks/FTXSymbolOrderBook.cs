﻿using CryptoExchange.Net.Objects;
using CryptoExchange.Net.OrderBook;
using CryptoExchange.Net.Sockets;
using Force.Crc32;
using FTX.Net.Objects;
using FTX.Net.Objects.Spot;
using FTX.Net.Objects.Spot.Socket;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace FTX.Net.SymbolOrderBooks
{
    /// <summary>
    /// Symbol order book
    /// </summary>
    public class FTXSymbolOrderBook : SymbolOrderBook
    {
        private FTXSocketClient _socketClient;
        private int? _grouping;

        public FTXSymbolOrderBook(string symbol, FTXSymbolOrderBookOptions? options = null) : base(symbol, options ?? new FTXSymbolOrderBookOptions())
        {
            _socketClient = new FTXSocketClient(new FTXSocketClientOptions
            {
                LogLevel = options?.LogLevel ?? LogLevel.Information
            });
            _grouping = options?.Grouping;
        }

        protected override async Task<CallResult<UpdateSubscription>> DoStartAsync()
        {
            CallResult<UpdateSubscription> subResult;
            if (_grouping.HasValue)
            {
                subResult = await _socketClient.SubscribeToGroupedOrderBookUpdatesAsync(Symbol, _grouping.Value, DataHandler).ConfigureAwait(false);
                if (!subResult)
                    return subResult;
            }
            else
            {
                subResult = await _socketClient.SubscribeToOrderBookUpdatesAsync(Symbol, DataHandler).ConfigureAwait(false);
                if (!subResult)
                    return subResult;
            }

            var setResult = await WaitForSetOrderBookAsync(10000).ConfigureAwait(false);
            return setResult ? subResult : new CallResult<UpdateSubscription>(null, setResult.Error);
        }

        private void DataHandler(DataEvent<FTXStreamOrderBook> update)
        {
            if (update.Data.Action == "partial")
            {
                SetInitialOrderBook(update.Data.Time.Ticks, update.Data.Bids, update.Data.Asks);
                if(!_grouping.HasValue)
                    AddChecksum((int)update.Data.Checksum); // ?
            }
            else
            {
                UpdateOrderBook(update.Data.Time.Ticks, update.Data.Bids, update.Data.Asks);
                if(!_grouping.HasValue)
                    AddChecksum((int)update.Data.Checksum); // ?
            }
        }

        protected override bool DoChecksum(int checksum)
        {
            //var checksumString = "";
            //for(var i = 0; i < 100; i++)
            //{
            //    if (bids.Count > i)
            //    {
            //        var bid = bids.ElementAt(i).Value;
            //        checksumString += $"{bid.Price.ToString(CultureInfo.InvariantCulture)}:{bid.Quantity.ToString(CultureInfo.InvariantCulture)}:";
            //    }
            //    if (asks.Count > i)
            //    {
            //        var ask = asks.ElementAt(i).Value;
            //        checksumString += $"{ask.Price.ToString(CultureInfo.InvariantCulture)}:{ask.Quantity.ToString(CultureInfo.InvariantCulture)}:";
            //    }
            //}

            //checksumString = checksumString.TrimEnd(':');

            // TODO Can't seem to be able to calculate the correct checksum..

            return true;
        }

        protected override async Task<CallResult<bool>> DoResyncAsync()
        {
            return await WaitForSetOrderBookAsync(10000).ConfigureAwait(false);
        }

        public override void Dispose()
        {
            processBuffer.Clear();
            asks.Clear();
            bids.Clear();

            _socketClient?.Dispose();
        }
    }
}
