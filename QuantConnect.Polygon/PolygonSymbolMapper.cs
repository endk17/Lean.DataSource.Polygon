﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Brokerages;
using System.Globalization;

namespace QuantConnect.Polygon
{
    /// <summary>
    /// Provides the mapping between Lean symbols and Polygon.io symbols.
    /// </summary>
    public class PolygonSymbolMapper : ISymbolMapper
    {
        private readonly Dictionary<string, Symbol> _leanSymbolsCache= new();

        /// <summary>
        /// Converts a Lean symbol instance to a brokerage symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The brokerage symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value))
            {
                throw new ArgumentException($"Invalid symbol: {(symbol == null ? "null" : symbol.ToString())}");
            }

            switch (symbol.SecurityType)
            {
                case SecurityType.Equity:
                case SecurityType.Index:
                    return symbol.Value.Replace(" ", "");

                case SecurityType.Option:
                case SecurityType.IndexOption:
                    return $"O:{symbol.Value.Replace(" ", "")}";

                default:
                    throw new Exception($"PolygonSymbolMapper.GetBrokerageSymbol(): unsupported security type: {symbol.SecurityType}");
            }
        }

        /// <summary>
        /// Converts a brokerage symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The brokerage symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market,
            DateTime expirationDate = new DateTime(), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            return GetLeanSymbol(brokerageSymbol, securityType, market, OptionStyle.American, expirationDate, strike, optionRight);
        }

        /// <summary>
        /// Converts a brokerage symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The brokerage symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="optionStyle">The option style</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, OptionStyle optionStyle,
            DateTime expirationDate = new DateTime(), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                throw new ArgumentException("Invalid symbol: " + brokerageSymbol);
            }

            switch (securityType)
            {
                case SecurityType.Option:
                    return Symbol.CreateOption(brokerageSymbol, market, optionStyle, optionRight, strike, expirationDate);

                case SecurityType.IndexOption:
                    return Symbol.CreateOption(Symbol.Create(brokerageSymbol, SecurityType.Index, market), market, optionStyle, optionRight,
                        strike, expirationDate);

                case SecurityType.Equity:
                    return Symbol.Create(brokerageSymbol, securityType, market);

                case SecurityType.Forex:
                    return Symbol.Create(brokerageSymbol.Replace("/", ""), securityType, market);

                case SecurityType.Crypto:
                    return Symbol.Create(brokerageSymbol.Replace("-", ""), securityType, market);

                default:
                    throw new Exception($"PolygonSymbolMapper.GetLeanSymbol(): unsupported security type: {securityType}");
            }
        }

        /// <summary>
        /// Gets the Lean symbol for the specified Polygon symbol
        /// </summary>
        /// <param name="polygonSymbol">The polygon symbol</param>
        /// <returns>The corresponding Lean symbol</returns>
        public Symbol GetLeanSymbol(string polygonSymbol)
        {
            lock (_leanSymbolsCache)
            {
                if (!_leanSymbolsCache.TryGetValue(polygonSymbol, out var symbol))
                {
                    if (polygonSymbol.StartsWith("O:"))
                    {
                        symbol = GetLeanOptionSymbol(polygonSymbol);
                    }
                    else
                    {
                        symbol = GetLeanSymbol(polygonSymbol, SecurityType.Equity, Market.USA);
                    }
                    _leanSymbolsCache[polygonSymbol] = symbol;
                }

                return symbol;
            }
        }

        /// <summary>
        /// Gets the Lean option symbol for the specified Polygon symbol
        /// </summary>
        /// <param name="polygonSymbol">The polygon symbol</param>
        /// <returns>The corresponding Lean option symbol</returns>
        private Symbol GetLeanOptionSymbol(string polygonSymbol)
        {
            // Polygon option symbol format, without the "O:" prefix, is similar to OSI option symbol format
            // But they don't have a fixed number of characters for the underlying ticker, so we need to parse it
            // starting from the end of the string: strike -> option right -> expiration date -> underlying ticker.
            // Reference: https://polygon.io/blog/how-to-read-a-stock-options-ticker
            var strike = Int64.Parse(polygonSymbol.Substring(polygonSymbol.Length - 8)) / 1000m;
            var optionRight = polygonSymbol.Substring(polygonSymbol.Length - 9, 1) == "C" ? OptionRight.Call : OptionRight.Put;
            var expirationDate = DateTime.ParseExact(polygonSymbol.Substring(polygonSymbol.Length - 15, 6), "yyMMdd", CultureInfo.InvariantCulture);
            var underlyingTicker = polygonSymbol.Substring(2, polygonSymbol.Length - 15 - 2);

            var symbol = Symbol.CreateOption(Symbol.Create(underlyingTicker, SecurityType.Equity, Market.USA), Market.USA, OptionStyle.American,
                optionRight, strike, expirationDate);
            _leanSymbolsCache[polygonSymbol] = symbol;

            return symbol;
        }
    }
}
