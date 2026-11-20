# Blacksmith Temper and Fortune Teller Charge

Barry now offers Temper alongside Buy, Sell and Farewell. Select a carried sword, shield or Trained Wand, review the resulting modifier and price, then confirm. Ordinary wands, Troll Hammers and Bow Guns are excluded. Each transaction adds exactly one modifier point, preserves identification/curse/equipped flags and cannot exceed +99. The resulting level determines cost: +0 -> +1 costs 100 G, +2 -> +3 costs 300 G, +98 -> +99 costs 9900 G. Negative modifiers have a minimum service charge of 100 G per step. Both item changes and payment persist together. Inventory slots do not change.

The Fortune Teller offers Charge for carried balls, at 200 G per charge. It uses the existing recharge cap of nine charges, including empty balls. Both services show a confirmation, reject insufficient funds and stale selections, and do not consume tower turns. These are added gameplay services; original NPC dialogue remains intact.

Validation: runtime assembly compiles. Isolated Unity probes cover item eligibility, caps, exact and insufficient payment, stale confirmations, equipped flags, persistence, inventory count and service navigation. Receipts: Validation/NpcRestaurant/BarryTemper-passed.txt and ShielaCharge-passed.txt. Full main-scene UI not visually checked in these changes.
