using System;
using NUnit.Framework;
using AzureDreamsReimagined;
public class TowerDamageLedgerTests
{
 [Test] public void PlayerAndFamiliarHitsKeepIndependentTargets()
 {
  var ledger=new TowerDamageLedger();ledger.Add(0,3);ledger.Add(1,2);
  Assert.That(ledger.ForTarget(0),Is.EqualTo(3));Assert.That(ledger.ForTarget(1),Is.EqualTo(2));Assert.That(ledger.ForTarget(2),Is.Zero);
 }
 [Test] public void SharedTargetCombinesHitsAndNextTurnStartsEmpty()
 {
  var ledger=new TowerDamageLedger();ledger.Add(0,3);ledger.Add(0,2);Assert.That(ledger.ForTarget(0),Is.EqualTo(5));
  ledger.Clear();Assert.That(ledger.ForTarget(0),Is.Zero);ledger.Add(1,4);Assert.That(ledger.ForTarget(1),Is.EqualTo(4));Assert.That(ledger.ForTarget(0),Is.Zero);
 }
 [Test] public void InvalidCommitDoesNotChangeExistingDamage()
 {
  var ledger=new TowerDamageLedger();ledger.Add(0,3);
  Assert.Throws<ArgumentOutOfRangeException>(()=>ledger.Add(-1,2));Assert.Throws<ArgumentOutOfRangeException>(()=>ledger.Add(0,-1));Assert.That(ledger.ForTarget(0),Is.EqualTo(3));
 }
}
