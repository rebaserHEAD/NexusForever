using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using NexusForever.Database;
using NexusForever.Database.Character;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Trade;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Reward;
using NexusForever.Game.Static.Trade;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;
using NexusForever.Shared;
using TradeResult = NexusForever.Network.World.Message.Model.ServerP2PTradeResult.P2PTradeResult;

namespace NexusForever.Game.Trade
{
    public class TradeManager : ITradeManager
    {
        // the client's trade window has 8 slots for each side
        private const int MaxItemsPerSide = 8;
        // the client refuses to start a trade further apart than this, after subtracting both bounding radii
        private const float MaxDistance = 6.0f;
        // players all have the default hit radius, not the client's bounding radius, and move between the client's check and the request
        private const float DistanceTolerance = 1.0f;

        // a commit sent this soon after a change may have left the client before the change reached it
        private static readonly TimeSpan CommitLockout = TimeSpan.FromSeconds(1d);
        // the client never expires an invite, and its report button hides one without declining it
        private static readonly TimeSpan InviteExpiry = TimeSpan.FromSeconds(60d);
        // longest wait for a save already in progress before the exchange gives up
        private static readonly TimeSpan SaveWaitLimit = TimeSpan.FromSeconds(10d);
        // an exchange still being written after this long holds both players and is logged
        private static readonly TimeSpan ExecutingWarning = TimeSpan.FromSeconds(30d);

        // a trade is indexed under both characters, a character is in at most one trade
        private readonly Dictionary<Identity, ITrade> tradesByCharacter = [];
        private readonly List<ITrade> trades = [];

        #region Dependency Injection

        private readonly ILogger<TradeManager> log;
        private readonly IDatabaseManager databaseManager;
        private readonly IFactory<ITrade> tradeFactory;

        public TradeManager(
            ILogger<TradeManager> log,
            IDatabaseManager databaseManager,
            IFactory<ITrade> tradeFactory)
        {
            this.log             = log;
            this.databaseManager = databaseManager;
            this.tradeFactory    = tradeFactory;
        }

        #endregion

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occurred.
        /// </summary>
        public void Update(double lastTick)
        {
            // iterated backwards, a trade that ends is removed from the list
            for (int i = trades.Count - 1; i >= 0; i--)
            {
                ITrade trade = trades[i];
                if (trade.State == TradeState.Executing)
                {
                    UpdateExecuting(trade);
                    continue;
                }

                if (trade.State == TradeState.Invited && Stopwatch.GetElapsedTime(trade.CreatedTimestamp) >= InviteExpiry)
                {
                    EndTrade(trade, TradeResult.PlayerDeclinedInvite, TradeResult.PlayerCanceled, "invite expired");
                    continue;
                }

                TradeResult? endResult = GetEndResult(trade);
                if (endResult.HasValue)
                {
                    EndTrade(trade, endResult.Value, endResult.Value, "a player can no longer trade");
                    continue;
                }

                if (trade.State == TradeState.Active)
                    WithdrawInvalidOffers(trade);
                else if (trade.State == TradeState.Committed)
                    TryExecute(trade);
            }
        }

        /// <summary>
        /// Invoked when <see cref="IPlayer"/> logs out, ending any trade they are part of unless its exchange is being written.
        /// </summary>
        public void OnLogout(IPlayer player)
        {
            if (!tradesByCharacter.TryGetValue(player.Identity, out ITrade trade))
                return;

            // an exchange being written completes and releases the holds before the player is cleaned up
            if (trade.State == TradeState.Executing)
                return;

            EndTrade(trade, TradeResult.PlayerLogOut, TradeResult.PlayerLogOut, $"{player.Name} logged out");
        }

        /// <summary>
        /// Invite the player with the supplied unit id to trade with <see cref="IPlayer"/>.
        /// </summary>
        public void InitiateTrade(IPlayer player, uint targetUnitId)
        {
            if (tradesByCharacter.TryGetValue(player.Identity, out ITrade existing))
            {
                // packets aren't handled while held, so an executing trade can't get here
                if (existing.State == TradeState.Executing)
                    return;

                // the client only initiates when it holds no trade of its own, so the one on the server is stale
                // matched by identity, not by player object, so the partner is found whichever object the trade holds
                ITradeParticipant other = existing.Initiator.Player.Identity == player.Identity ? existing.Target : existing.Initiator;
                RemoveTrade(existing);
                SendResult(other.Player, TradeResult.PlayerCanceled, true);
                log.LogTrace($"Trade between {existing.Initiator.Player.Name} and {existing.Target.Player.Name} ended, {player.Name} started another trade.");
            }

            IPlayer target = player.GetVisible<IPlayer>(targetUnitId);
            if (target == null)
            {
                SendResult(player, TradeResult.MissingPlayer, true);
                return;
            }

            if (target == player)
            {
                SendResult(player, TradeResult.ErrorInitiating, true);
                return;
            }

            if (!HasTradingRights(player))
            {
                SendResult(player, TradeResult.InviteFailedNoTrading, true);
                return;
            }

            if (tradesByCharacter.ContainsKey(target.Identity))
            {
                SendResult(player, TradeResult.TargetBusy, true);
                return;
            }

            if (!HasTradingRights(target))
            {
                SendResult(player, TradeResult.TargetNotAllowedToTrade, true);
                return;
            }

            if (!CanTrade(player) || !CanTrade(target) || !CanSeeEachOther(player, target) || !IsInRange(player, target))
            {
                log.LogTrace($"Trade request from {player.Name} to {target.Name} refused, a player can't trade right now or they are too far apart.");
                SendResult(player, TradeResult.ErrorInitiating, true);
                return;
            }

            ITrade trade = tradeFactory.Resolve();
            trade.Initialise(player, target);
            trade.SetState(TradeState.Invited);
            trades.Add(trade);
            tradesByCharacter.Add(player.Identity, trade);
            tradesByCharacter.Add(target.Identity, trade);

            target.Session.EnqueueMessageEncrypted(new ServerP2PTradeInvite
            {
                TradeInviterUnitId = player.Guid
            });

            log.LogTrace($"Trade between {player.Name} and {target.Name} created, {target.Name} invited.");
        }

        /// <summary>
        /// Accept the trade <see cref="IPlayer"/> has been invited to.
        /// </summary>
        public void AcceptInvite(IPlayer player)
        {
            if (!TryGetInvite(player, out ITrade trade))
                return;

            IPlayer initiator = trade.Initiator.Player;
            if (!CanTrade(initiator) || !CanTrade(player)
                || !HasTradingRights(initiator) || !HasTradingRights(player)
                || !CanSeeEachOther(initiator, player) || !IsInRange(initiator, player))
            {
                EndTrade(trade, TradeResult.ErrorInitiating, TradeResult.ErrorInitiating, $"{player.Name} accepted but a player can't trade right now or they are too far apart");
                return;
            }

            trade.SetState(TradeState.Active);
            trade.OnOfferChanged();
            SendResult(trade, TradeResult.PlayerAcceptedInvite, false);

            log.LogTrace($"Trade between {initiator.Name} and {player.Name} accepted.");
        }

        /// <summary>
        /// Decline the trade <see cref="IPlayer"/> has been invited to.
        /// </summary>
        public void DeclineInvite(IPlayer player)
        {
            if (!TryGetInvite(player, out ITrade trade))
                return;

            // the decliner gets PlayerCanceled, the client shows a "declined" notice to whoever receives PlayerDeclinedInvite
            EndTrade(trade, TradeResult.PlayerDeclinedInvite, TradeResult.PlayerCanceled, $"{player.Name} declined");
        }

        /// <summary>
        /// Cancel the trade <see cref="IPlayer"/> is part of.
        /// </summary>
        public void CancelTrade(IPlayer player)
        {
            // the client sends a cancel and a decline whenever its trade window closes, so usually there is no trade
            // a trade still being written when its player logged out holds the previous player object until the write completes
            if (!tradesByCharacter.TryGetValue(player.Identity, out ITrade trade) || trade.GetParticipant(player) == null)
            {
                log.LogTrace($"Trade cancel from {player.Name} ignored, not in a trade.");
                return;
            }

            if (trade.State == TradeState.Executing)
                return;

            if (trade.State == TradeState.Invited && trade.Target.Player == player)
                EndTrade(trade, TradeResult.PlayerDeclinedInvite, TradeResult.PlayerCanceled, $"{player.Name} declined");
            else
                EndTrade(trade, TradeResult.PlayerCanceled, TradeResult.PlayerCanceled, $"{player.Name} cancelled");
        }

        /// <summary>
        /// Offer the item with the supplied guid from the inventory of <see cref="IPlayer"/>.
        /// </summary>
        public void AddItem(IPlayer player, ulong itemGuid)
        {
            if (!TryGetActiveTrade(player, out ITrade trade, out ITradeParticipant participant))
                return;

            if (participant.Items.Count >= MaxItemsPerSide)
            {
                log.LogTrace($"Trade item {itemGuid} from {player.Name} refused, already offering {MaxItemsPerSide} items.");
                return;
            }

            if (participant.Items.Exists(i => i.Guid == itemGuid))
            {
                log.LogTrace($"Trade item {itemGuid} from {player.Name} refused, already offered.");
                return;
            }

            IItem item = player.Inventory.GetItem(itemGuid);
            if (!IsTradeable(item))
            {
                log.LogTrace($"Trade item {itemGuid} from {player.Name} refused, not in their inventory bags.");
                return;
            }

            var tradeItem = new TradeItem(trade.NextTradeIndex(), item.Guid, item.Id, item.StackCount);
            participant.Items.Add(tradeItem);
            trade.OnOfferChanged();

            trade.Send(new ServerP2PTradeUpdateItem
            {
                TradeIndex  = tradeItem.TradeIndex,
                OwnerUnitId = player.Guid,
                Item2Id     = tradeItem.Item2Id,
                ItemGuid    = tradeItem.Guid,
                Quantity    = tradeItem.Quantity
            });
        }

        /// <summary>
        /// Withdraw the offered item with the supplied guid.
        /// </summary>
        public void RemoveItem(IPlayer player, ulong itemGuid)
        {
            if (!TryGetActiveTrade(player, out ITrade trade, out ITradeParticipant participant))
                return;

            int index = participant.Items.FindIndex(i => i.Guid == itemGuid);
            if (index == -1)
            {
                log.LogTrace($"Trade item removal {itemGuid} from {player.Name} refused, not one of their offers.");
                return;
            }

            participant.Items.RemoveAt(index);
            trade.OnOfferChanged();

            trade.Send(new ServerPTPTradeItemRemoved
            {
                ItemGuid = itemGuid
            });
        }

        /// <summary>
        /// Set the amount of credits <see cref="IPlayer"/> offers.
        /// </summary>
        public void SetMoney(IPlayer player, ulong credits)
        {
            if (!TryGetActiveTrade(player, out ITrade trade, out ITradeParticipant participant))
                return;

            if (!player.CurrencyManager.CanAfford(CurrencyType.Credits, credits))
            {
                log.LogTrace($"Trade money {credits} from {player.Name} refused, can't afford it.");
                return;
            }

            participant.Credits = credits;
            trade.OnOfferChanged();

            trade.Send(new ServerP2PTradeUpdateMoney
            {
                UnitId  = player.Guid,
                Credits = credits
            });
        }

        /// <summary>
        /// Commit <see cref="IPlayer"/> to the trade, or withdraw the commit if already committed.
        /// </summary>
        public void Commit(IPlayer player)
        {
            if (!TryGetActiveTrade(player, out ITrade trade, out ITradeParticipant participant))
                return;

            if (participant.Committed)
            {
                participant.Committed = false;
                SendResult(trade, GetUncommitResult(participant), false);
                return;
            }

            if (Stopwatch.GetElapsedTime(trade.LastChangeTimestamp) < CommitLockout)
            {
                log.LogTrace($"Trade commit from {player.Name} ignored, it arrived within {CommitLockout.TotalSeconds}s of a change.");
                return;
            }

            ITradeParticipant partner = trade.GetPartner(participant);
            if (!partner.Committed)
            {
                if (!HasRoom(trade, participant))
                {
                    player.SendGenericError(GenericError.ItemInventoryFull);
                    return;
                }

                participant.Committed = true;
                SendResult(trade, GetCommitResult(participant), false);
                return;
            }

            // final commit, nothing moves unless everything is still valid now
            TradeResult? endResult = GetEndResult(trade);
            if (endResult.HasValue)
            {
                EndTrade(trade, endResult.Value, endResult.Value, "a player can no longer trade");
                return;
            }

            if (participant.IsOfferEmpty && partner.IsOfferEmpty)
            {
                EndTrade(trade, TradeResult.NothingToTrade, TradeResult.NothingToTrade, "nothing on offer");
                return;
            }

            if (WithdrawInvalidOffers(trade))
                return;

            foreach (ITradeParticipant receiver in trade.GetParticipants())
            {
                if (HasRoom(trade, receiver))
                    continue;

                // the partner's commit is withdrawn so both players see the trade is waiting again
                partner.Committed = false;
                SendResult(trade, GetUncommitResult(partner), false);
                receiver.Player.SendGenericError(GenericError.ItemInventoryFull);
                return;
            }

            participant.Committed = true;
            SendResult(trade, GetCommitResult(participant), false);

            trade.SetState(TradeState.Committed);
            TryExecute(trade);
        }

        private void TryExecute(ITrade trade)
        {
            // both characters are written together, so neither may have a save of its own in flight
            if (trade.Initiator.Player.IsSaving || trade.Target.Player.IsSaving)
            {
                if (Stopwatch.GetElapsedTime(trade.StateTimestamp) >= SaveWaitLimit)
                {
                    log.LogWarning($"Trade between {trade.Initiator.Player.Name} and {trade.Target.Player.Name} waited {SaveWaitLimit.TotalSeconds}s for a save in progress.");
                    EndTrade(trade, TradeResult.DbFailedToInit, TradeResult.DbFailedToInit, "waited too long for a save in progress");
                }

                return;
            }

            foreach (ITradeParticipant participant in trade.GetParticipants())
            {
                if (HasInvalidOffer(participant) || !participant.Player.CurrencyManager.CanAfford(CurrencyType.Credits, participant.Credits))
                {
                    EndTrade(trade, TradeResult.ErrorRemovingItem, TradeResult.ErrorRemovingItem, $"an offer from {participant.Player.Name} changed before the exchange");
                    return;
                }

                if (!HasRoom(trade, participant))
                {
                    EndTrade(trade, TradeResult.ErrorAddingItem, TradeResult.ErrorAddingItem, $"{participant.Player.Name} has no room before the exchange");
                    return;
                }
            }

            foreach (ITradeParticipant participant in trade.GetParticipants())
                trade.SaveHolds.Add(participant.Player.HoldSave());

            trade.SetState(TradeState.Executing);
            ApplyExchange(trade);

            // one context and one SaveChangesAsync, so both characters are written in a single transaction
            trade.SaveTask = databaseManager.GetDatabase<CharacterDatabase>().Save([trade.Initiator.Player, trade.Target.Player]);
        }

        private void ApplyExchange(ITrade trade)
        {
            // every outgoing item leaves before any arrives, the room checks count outgoing items as free slots
            foreach (ITradeParticipant from in trade.GetParticipants())
            {
                ITradeParticipant to = trade.GetPartner(from);
                foreach (TradeItem tradeItem in from.Items)
                {
                    IItem item = from.Player.Inventory.GetItem(tradeItem.Guid);
                    from.Player.Inventory.ItemRemove(item, ItemUpdateReason.Trade);
                    trade.ItemMoves.Add(new TradeItemMove(from, to, item));
                }
            }

            foreach (TradeItemMove move in trade.ItemMoves)
            {
                move.Item.CharacterId = move.To.Player.CharacterId;
                move.To.Player.Inventory.AddItem(move.Item, InventoryLocation.Inventory, ItemUpdateReason.Trade);
            }

            foreach (ITradeParticipant from in trade.GetParticipants())
            {
                if (from.Credits == 0ul)
                    continue;

                from.Player.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, from.Credits);
                trade.GetPartner(from).Player.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, from.Credits);
            }
        }

        private void RevertExchange(ITrade trade)
        {
            foreach (TradeItemMove move in trade.ItemMoves)
            {
                // only a server side item creation during the hold can fill the giver's bag, the item then stays with the receiver
                if (move.From.Player.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory) == 0u)
                {
                    log.LogError($"Trade revert couldn't return item {move.Item.Guid} to {move.From.Player.Name} ({move.From.Player.CharacterId}), it stays with {move.To.Player.Name} ({move.To.Player.CharacterId}).");
                    continue;
                }

                move.To.Player.Inventory.ItemRemove(move.Item, ItemUpdateReason.Trade);
                move.Item.CharacterId = move.From.Player.CharacterId;
                move.From.Player.Inventory.AddItem(move.Item, InventoryLocation.Inventory, ItemUpdateReason.Trade);
            }

            foreach (ITradeParticipant from in trade.GetParticipants())
            {
                if (from.Credits == 0ul)
                    continue;

                IPlayer to = trade.GetPartner(from).Player;
                if (!to.CurrencyManager.CanAfford(CurrencyType.Credits, from.Credits))
                {
                    log.LogError($"Trade revert couldn't return {from.Credits} credits from {to.Name} ({to.CharacterId}) to {from.Player.Name} ({from.Player.CharacterId}).");
                    continue;
                }

                to.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, from.Credits);
                from.Player.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, from.Credits);
            }
        }

        private void UpdateExecuting(ITrade trade)
        {
            // the write is shared by both players' sessions, so it's polled here rather than queued as an event on either session
            if (!trade.SaveTask.IsCompleted)
            {
                if (!trade.ExecutingWarned && Stopwatch.GetElapsedTime(trade.StateTimestamp) >= ExecutingWarning)
                {
                    trade.ExecutingWarned = true;
                    log.LogWarning($"Trade between {trade.Initiator.Player.Name} and {trade.Target.Player.Name} has been writing for {ExecutingWarning.TotalSeconds}s, both players are held.");
                }

                return;
            }

            if (trade.SaveTask.IsCompletedSuccessfully)
            {
                SendResult(trade, TradeResult.FinishedSuccess, true);

                // Info rather than Trace, it's the only record of items and credits changing hands at default log levels
                log.LogInformation($"Trade between {trade.Initiator.Player.Name} and {trade.Target.Player.Name} completed. {GetManifest(trade)}");
            }
            else
            {
                // the in-memory exchange is undone before the holds are released, otherwise the next save of either player would write half of it
                log.LogError(trade.SaveTask.Exception, $"Trade between {trade.Initiator.Player.Name} and {trade.Target.Player.Name} failed to save and was reverted. {GetManifest(trade)}");
                RevertExchange(trade);
                SendResult(trade, TradeResult.DbFailed, true);
            }

            foreach (IDisposable hold in trade.SaveHolds)
                hold.Dispose();

            RemoveTrade(trade);
        }

        private bool WithdrawInvalidOffers(ITrade trade)
        {
            bool withdrawn = false;
            foreach (ITradeParticipant participant in trade.GetParticipants())
            {
                // iterated backwards, a withdrawn item is removed from the list
                for (int i = participant.Items.Count - 1; i >= 0; i--)
                {
                    TradeItem tradeItem = participant.Items[i];
                    if (IsOfferValid(participant, tradeItem))
                        continue;

                    participant.Items.RemoveAt(i);
                    trade.Send(new ServerPTPTradeItemRemoved
                    {
                        ItemGuid = tradeItem.Guid
                    });

                    withdrawn = true;
                    log.LogTrace($"Trade item {tradeItem.Guid} from {participant.Player.Name} withdrawn, it moved or its stack changed.");
                }

                if (participant.Credits != 0ul && !participant.Player.CurrencyManager.CanAfford(CurrencyType.Credits, participant.Credits))
                {
                    participant.Credits = 0ul;
                    trade.Send(new ServerP2PTradeUpdateMoney
                    {
                        UnitId  = participant.Player.Guid,
                        Credits = 0ul
                    });

                    withdrawn = true;
                    log.LogTrace($"Trade money from {participant.Player.Name} withdrawn, they can no longer afford it.");
                }
            }

            if (withdrawn)
                trade.OnOfferChanged();

            return withdrawn;
        }

        private void EndTrade(ITrade trade, TradeResult initiatorResult, TradeResult targetResult, string reason)
        {
            RemoveTrade(trade);

            SendResult(trade.Initiator.Player, initiatorResult, true);
            SendResult(trade.Target.Player, targetResult, true);

            log.LogTrace($"Trade between {trade.Initiator.Player.Name} and {trade.Target.Player.Name} ended, {reason}.");
        }

        private void RemoveTrade(ITrade trade)
        {
            trades.Remove(trade);

            foreach (ITradeParticipant participant in trade.GetParticipants())
                if (tradesByCharacter.TryGetValue(participant.Player.Identity, out ITrade indexed) && indexed == trade)
                    tradesByCharacter.Remove(participant.Player.Identity);
        }

        private void SendResult(ITrade trade, TradeResult result, bool cancelled)
        {
            foreach (ITradeParticipant participant in trade.GetParticipants())
                SendResult(participant.Player, result, cancelled);
        }

        private void SendResult(IPlayer player, TradeResult result, bool cancelled)
        {
            // a player whose session has dropped has nothing left to tell
            if (player.LogoutManager.State == LogoutState.Finished)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerP2PTradeResult
            {
                Result    = result,
                Cancelled = cancelled
            });

            log.LogTrace($"Trade result {result} (cancelled {cancelled}) sent to {player.Name}.");
        }

        private bool TryGetInvite(IPlayer player, out ITrade trade)
        {
            if (!tradesByCharacter.TryGetValue(player.Identity, out trade)
                || trade.State != TradeState.Invited
                || trade.Target.Player != player)
            {
                log.LogTrace($"Trade invite answer from {player.Name} ignored, not invited to a trade.");
                return false;
            }

            return true;
        }

        private bool TryGetActiveTrade(IPlayer player, out ITrade trade, out ITradeParticipant participant)
        {
            participant = null;

            // the client drops changes once both players have committed, so they are ignored here too
            if (!tradesByCharacter.TryGetValue(player.Identity, out trade) || trade.State != TradeState.Active)
            {
                log.LogTrace($"Trade request from {player.Name} ignored, not in an open trade.");
                return false;
            }

            // a trade still being written when its player logged out holds the previous player object until the write completes
            participant = trade.GetParticipant(player);
            if (participant == null)
            {
                log.LogTrace($"Trade request from {player.Name} ignored, their trade is from before a relog.");
                return false;
            }

            return true;
        }

        private static TradeResult? GetEndResult(ITrade trade)
        {
            IPlayer initiator = trade.Initiator.Player;
            IPlayer target    = trade.Target.Player;

            foreach (ITradeParticipant participant in trade.GetParticipants())
                if (!participant.Player.CanTeleport() || participant.Player.IsLoading)
                    return TradeResult.MissingPlayer;

            // visibility is the leash once a trade has started, no result code means "too far"
            if (!CanSeeEachOther(initiator, target))
                return TradeResult.MissingPlayer;

            // logging out is ended from OnLogout, this catches every other reason a player can no longer trade
            foreach (ITradeParticipant participant in trade.GetParticipants())
                if (!CanTrade(participant.Player) || !HasTradingRights(participant.Player))
                    return TradeResult.PlayerCanceled;

            return null;
        }

        private static bool CanTrade(IPlayer player)
        {
            return player.LogoutManager.State is LogoutState.None or LogoutState.Timer
                && !player.IsLoading
                && player.CanTeleport()
                && player.IsAlive
                && !player.InCombat
                && player.PlatformGuid == null;
        }

        private static bool HasTradingRights(IPlayer player)
        {
            return player.EntitlementManager.GetEntitlement(EntitlementType.EconomyParticipation)?.Amount > 0u
                && player.Account.RewardPropertyManager.GetRewardProperty(RewardPropertyType.Trading)?.GetValue(0u) > 0f;
        }

        private static bool CanSeeEachOther(IPlayer first, IPlayer second)
        {
            return first.GetVisible<IPlayer>(second.Guid) != null && second.GetVisible<IPlayer>(first.Guid) != null;
        }

        private static bool IsInRange(IPlayer first, IPlayer second)
        {
            return Vector3.Distance(first.Position, second.Position) - first.HitRadius - second.HitRadius <= MaxDistance + DistanceTolerance;
        }

        private static bool IsTradeable(IItem item)
        {
            // binding isn't tracked by the server yet, the client's own check is the only bind rule
            return item != null && item.Location == InventoryLocation.Inventory;
        }

        private static bool IsOfferValid(ITradeParticipant owner, TradeItem tradeItem)
        {
            IItem item = owner.Player.Inventory.GetItem(tradeItem.Guid);
            return IsTradeable(item) && item.Id == tradeItem.Item2Id && item.StackCount == tradeItem.Quantity;
        }

        private static bool HasInvalidOffer(ITradeParticipant owner)
        {
            foreach (TradeItem tradeItem in owner.Items)
                if (!IsOfferValid(owner, tradeItem))
                    return true;

            return false;
        }

        private static bool HasRoom(ITrade trade, ITradeParticipant receiver)
        {
            // outgoing items leave before incoming items arrive, so their slots count as free
            uint freeSlots = receiver.Player.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory);
            return freeSlots + (uint)receiver.Items.Count >= (uint)trade.GetPartner(receiver).Items.Count;
        }

        private static TradeResult GetCommitResult(ITradeParticipant participant)
        {
            return participant.IsInitiator ? TradeResult.InitiatorCommitted : TradeResult.TargetCommitted;
        }

        private static TradeResult GetUncommitResult(ITradeParticipant participant)
        {
            return participant.IsInitiator ? TradeResult.InitiatorUnCommitted : TradeResult.TargetUnCommitted;
        }

        private static string GetManifest(ITrade trade)
        {
            return string.Join(" ", trade.GetParticipants().Select(p =>
                $"{p.Player.Name} ({p.Player.CharacterId}) gave {p.Credits} credits and [{string.Join(", ", p.Items.Select(i => $"{i.Guid}: {i.Item2Id} x{i.Quantity}"))}]."));
        }
    }
}
