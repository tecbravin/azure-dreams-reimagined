using NUnit.Framework;
using System.Collections.Generic;
using AzureDreamsReimagined;
public class TownHomeDialogueSelectorTests
{
 [Test] public void FirstConversationSetsFlagAndRepeatedConversationUsesFallback()
 {
  var flags=new HashSet<int>();
  var context=new TownHomeDialogueSelector.Context { Progress=5 };
  bool Read(int id)=>id==1 || (id!=0 && flags.Contains(id));
  void Set(int id)=>flags.Add(id);
  Assert.AreEqual(0x80017152u,TownHomeDialogueSelector.Select(context,Read,Set));
  Assert.IsTrue(flags.Contains(160)); Assert.IsTrue(flags.Contains(158));
  Assert.AreEqual(0x8001602Au,TownHomeDialogueSelector.Select(context,Read,Set));
 }
 [Test] public void PoolInvitationPrecedesGoldRecommendation()
 {
  var flags=new HashSet<int>{5216};
  Assert.AreEqual(0x800173E4u,TownHomeDialogueSelector.Select(
   new TownHomeDialogueSelector.Context{Progress=1,Gold=20000},
   id=>id==1 || flags.Contains(id), id=>flags.Add(id)));
  Assert.IsTrue(flags.Contains(159));
 }
 [Test] public void TempleInvitationRequiresNeitherSlotToContainSpecies18()
 {
  foreach(bool second in new[]{false,true})
  {
   var c=new TownHomeDialogueSelector.Context{Progress=1,HouseQuery=1,FamiliarA=second?0:18,FamiliarB=second?18:0};
   Assert.AreEqual(0x8001602Au,TownHomeDialogueSelector.Select(c,id=>id==1,id=>{}));
  }
 }
}
