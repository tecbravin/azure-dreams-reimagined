using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly List<(ActorRecord Actor,string Name)> _giveRecipients=new();
        private Action<ActorRecord> _giveRecipientAction;
        private int _giveRecipientIndex;
        private Vector2 _giveRecipientScroll;

        private bool GiveRecipientAvailable(ActorRecord actor)
        {
            if(actor==null || actor.Hp<=0 || IsFamiliarLost(actor)) return false;
            if(actor==_familiarActor) return _area==Area.Town ? _familiarDefinition : _familiar;
            if(actor==_blueCollarAlly?.Actor) return _blueCollarAlly.View;
            return _bagFamiliars.Exists(f=>f.Actor==actor && (_area==Area.Town || f.View));
        }

        private void OpenGiveRecipientPicker(DungeonItemEntry item,Action<ActorRecord> confirm)
        {
            _giveRecipients.Clear();
            void Add(ActorRecord actor,string name)
            {
                if(GiveRecipientAvailable(actor) && (!IsTrollWeapon(item)||IsTroll(actor)) && !_giveRecipients.Exists(f=>f.Actor==actor))
                    _giveRecipients.Add((actor,name));
            }
            Add(_familiarActor,FamiliarDisplayName);
            if(_blueCollarAlly!=null) Add(_blueCollarAlly.Actor,TowerCombatName(_blueCollarAlly.Actor));
            foreach(var familiar in _bagFamiliars) Add(familiar.Actor,familiar.Name);
            if(_giveRecipients.Count==0){_message=T("No familiar can receive this item.","Nenhum familiar pode receber este item.");return;}
            _giveRecipientIndex=0;_giveRecipientScroll=Vector2.zero;_giveRecipientAction=confirm;
        }

        private bool HandleGiveRecipientPicker()
        {
            if(_giveRecipientAction==null) return false;
            if(_cancel?.WasPressedThisFrame()==true || _inventoryAction?.WasPressedThisFrame()==true){_giveRecipientAction=null;return true;}
            int delta=MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
            _giveRecipientIndex=Mathf.Clamp(_giveRecipientIndex+delta,0,_giveRecipients.Count-1);
            if(delta!=0) _giveRecipientScroll.y=Mathf.Clamp(_giveRecipientScroll.y,_giveRecipientIndex*32f-128f,_giveRecipientIndex*32f);
            if(_interact?.WasPressedThisFrame()==true) ConfirmGiveRecipient(_giveRecipientIndex);
            return true;
        }

        private void ConfirmGiveRecipient(int index)
        {
            if(_giveRecipientAction==null || index<0 || index>=_giveRecipients.Count) return;
            var actor=_giveRecipients[index].Actor;
            var confirm=_giveRecipientAction;_giveRecipientAction=null;
            if(GiveRecipientAvailable(actor)) confirm(actor);
        }

        private void DrawGiveRecipientPicker(float width,float height)
        {
            if(_giveRecipientAction==null) return;
            var panel=new Rect((width-360f)*.5f,(height-230f)*.5f,360f,230f);
            DrawDialogueFrame(panel);
            DrawInventoryFittedText(new Rect(panel.x+10,panel.y+6,340,28),T("Choose a familiar","Escolha um familiar"),_bodyStyle);
            _giveRecipientScroll=GUI.BeginScrollView(new Rect(panel.x+10,panel.y+40,340,160),_giveRecipientScroll,new Rect(0,0,320,_giveRecipients.Count*32));
            int clicked=-1;
            for(int i=0;i<_giveRecipients.Count;i++) if(DrawInventoryFittedButton(new Rect(0,i*32,320,30),_giveRecipients[i].Name,i==_giveRecipientIndex)) clicked=i;
            GUI.EndScrollView();
            if(DrawInventoryFittedButton(new Rect(panel.x+10,panel.y+202,340,24),WithCancelHint("Back","Voltar"))) _giveRecipientAction=null;
            if(clicked>=0) ConfirmGiveRecipient(clicked);
        }

        private void GiveItemToRecipient(Inventory inventory,int index,DungeonItemEntry item,ActorRecord actor,Func<bool> remove,Action refresh,Func<bool> sourceAvailable=null)
        {
            bool Current()=>(sourceAvailable?.Invoke() ?? true) && GiveRecipientAvailable(actor) && index>=0 && index<inventory.DungeonItems.Count && inventory.DungeonItems[index].Kind==item.Kind && inventory.DungeonItems[index].Subtype==item.Subtype && inventory.DungeonItems[index].Param==item.Param && inventory.DungeonItems[index].Flags==item.Flags;
            if(!Current() || RefusesLevaFruit(item,actor)) return;
            if(IsTrollWeapon(item) && Inventory.IsCursedEquipment(actor.Weapon)){ShowLockedEquipmentMessage();return;}
            if(TeachesFamiliar(item))
            {
                if(TryGiveBallToLearn(inventory,index,item,remove,actor,refresh)) return;
            }
            if(!IsTrollWeapon(item) && !CanUseInventoryEffect(item,true,actor)) return;
            if(item.Kind==ItemKind.Fruit && item.Subtype==7 && !CanRevertToEgg(actor)) return;
            if(item.Kind==ItemKind.Ball && MonsterExhausted(actor)) return;
            ScheduleInventoryAction(()=>
            {
                if(!Current() || RefusesLevaFruit(item,actor)) return;
                if(IsTrollWeapon(item)) { if(!GiveTrollWeapon(inventory,index,actor,lockCursed:_area==Area.Tower)) return; }
                else
                {
                    if(item.Kind==ItemKind.Ball && item.Param>1)
                    {if(!inventory.ReplaceDungeonItem(index,ItemKind.Ball,new DungeonItemEntry(item.Subtype,item.Kind,(sbyte)(item.Param-1),item.Flags)))return;}
                    else if(!remove()) return;
                    ApplyInventoryEffect(item,actor);
                }
                refresh?.Invoke();
            },familiarCasting:actor==_familiarActor && item.Kind==ItemKind.Ball);
        }
    }
}
