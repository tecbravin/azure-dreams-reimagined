using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string CherrlWalkCompletedKey = StoryPrefix + "cherrl.walkHospital";
        private const string CherrlWalkApologyKey = StoryPrefix + "cherrl.walkApology";
        private bool _cherrlWalkActive, _cherrlWalkAnimating, _cherrlWalkTransitioning, _cherrlWalkCarrying;
        private int _cherrlWalkVersion;
        private NPCController _cherrlWalkCompanion;

        // TOWN.BIN 0x67B000–0x67B54B: actor 1 (Koh), 13 (Cherrl), 6 (Ghosh).
        // Script movement arguments are original 1/32-world-unit coordinates.
        private static readonly Vector3 CherrlWalkKohStart = new(108f,0f,42.75f);
        private static readonly Vector3 CherrlWalkGirlStart = new(107.5f,0f,43.5f);
        private static readonly Vector3 CherrlWalkKohStop = new(104f,0f,42.75f);
        private static readonly Vector3 CherrlWalkGirlStop = new(104.5f,0f,43.5f);
        private static readonly Vector3 CherrlWalkGhoshStop = new(101.75f,0f,43f);
        private static readonly Vector3 CherrlWalkGhoshExit = new(90f,0f,43f);
        private static readonly Vector3 CherrlWalkClinicApproach = new(32f,0f,42.75f);
        private static readonly Vector3 CherrlWalkClinicDoor = new(32f,0f,39f);

        private void CherrlWalkToHospital(NPCController cherrl)
        {
            if (_cherrlWalkActive || StoryStage("cherrl") != CherrlWalkRequest ||
                StoryRoom != CherrlHouse || !StoryBuilding("hospital") || _carriedItem != null || _heldTownPot) return;
            _cherrlWalkActive = _cherrlWalkAnimating = true;
            StartCoroutine(PlayCherrlWalkDeparture(_cherrlWalkVersion));
        }

        private IEnumerator PlayCherrlWalkDeparture(int version)
        {
            _cherrlWalkTransitioning = true;
            // The interior height map is still installed until LeaveInterior finishes.
            _outsidePosition = CherrlWalkKohStart;
            _outsideFacing = Vector2.left;
            yield return LeaveInterior();
            _cherrlWalkTransitioning = false;
            if (version != _cherrlWalkVersion || StoryRoom.Length != 0 || !_player) yield break;
            _player.transform.position = RetailTownActorPosition(CherrlWalkKohStart,TownActorFootOffset);
            UpdateCameraFollow(snap:true);
            _cherrlWalkAnimating = true;
            SpawnStoryResident("cherrl","cherrl","Cherrl",new Vector2Int(108,44),
                RetailTownActorPosition(CherrlWalkGirlStart,0f));
            var companion = FindStoryActor("cherrl");
            if (!companion) { ResetCherrlWalk(); yield break; }
            _cherrlWalkCompanion = companion;
            companion.enabled = false;
            RetailResidentBanks.ApplyBank(companion.gameObject,"cheril_window");
            yield return WalkCherrlPair(CherrlWalkKohStop,CherrlWalkGirlStop,1.4f,version);
            if (version != _cherrlWalkVersion || !companion) yield break;
            companion.enabled = true;
            companion.transform.rotation = Quaternion.LookRotation(Vector3.back);
            _cherrlWalkAnimating = false;
            CherrlOutdoorWalk(companion);
        }

        private IEnumerator WalkCherrlPair(Vector3 koh, Vector3 girl, float duration, int version)
        {
            var actor = _cherrlWalkCompanion;
            if (!_player || !actor) yield break;
            var from = _player.transform.position;
            var girlFrom = actor.transform.position;
            koh = RetailTownActorPosition(koh,TownActorFootOffset);
            girl = RetailTownActorPosition(girl,0f);
            for (float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                if (version != _cherrlWalkVersion || !_player || !actor) yield break;
                float progress = Mathf.Clamp01(t/duration);
                _player.transform.position = Vector3.Lerp(from,koh,progress);
                actor.transform.position = Vector3.Lerp(girlFrom,girl,progress);
                _playerLocomotion?.Present(koh-from,t*4,true);
                actor.GetComponent<ActorLocomotionView>()?.Present(girl-girlFrom,t*4,true);
                yield return null;
            }
            if (version != _cherrlWalkVersion || !_player || !actor) yield break;
            _player.transform.position = koh; actor.transform.position = girl;
            _playerCell = new Vector2Int(Mathf.RoundToInt(koh.x),Mathf.RoundToInt(koh.z));
            _playerLocomotion?.Present(koh-from,0,false);
            actor.GetComponent<ActorLocomotionView>()?.Present(girl-girlFrom,0,false);
            RefreshTownNpcCells();
        }

        private void CherrlOutdoorGhosh(NPCController cherrl)
        {
            if (!_cherrlWalkActive || _cherrlWalkAnimating) return;
            _cherrlWalkAnimating = true;
            StartCoroutine(PlayCherrlGhoshApproach(cherrl,_cherrlWalkVersion));
        }

        private IEnumerator PlayCherrlGhoshApproach(NPCController cherrl,int version)
        {
            yield return new WaitForSecondsRealtime(30f/60f); // native wait 0x1E
            if (version != _cherrlWalkVersion || !cherrl) yield break;
            var ghosh = FindStoryActor("cherrl_walk_ghosh");
            if (!ghosh)
            {
                SpawnStoryResident("ghosh","cherrl_walk_ghosh","Ghosh",new Vector2Int(90,43),
                    RetailTownActorPosition(CherrlWalkGhoshExit,0f));
                ghosh = FindStoryActor("cherrl_walk_ghosh");
            }
            if (!ghosh) { ResetCherrlWalk(); yield break; }
            ghosh.enabled = false;
            yield return PlayStoryWalk(ghosh,RetailTownActorPosition(CherrlWalkGhoshStop,0f),3f);
            if (version != _cherrlWalkVersion || !ghosh || !cherrl) yield break;
            ghosh.enabled = true;
            PlayAreaMusic("GhoshTheme.mp3");
            _cherrlWalkAnimating = false;
            GhoshSequence(ghosh, () => CherrlTalkSequence(cherrl,
                () => { _cherrlWalkAnimating=true; StartCoroutine(PlayCherrlFaint(cherrl,ghosh,version)); },
                new CherrlStoryLine(1,"{name} is not an airhead.\n{name}'s no airhead.","O {name} não é um cabeça de vento.\nNão é, não."),
                new CherrlStoryLine(1,"{name} is, {name} is ...","O {name} é... O {name} é...")),
                ("Hey {name}.\nWho's that cute girl beside you?","Ei, {name}.\nQuem é essa garota bonita do seu lado?"),
                ("Miss,","Senhorita,"),
                ("if you stand too close to him, you might catch his airhead disease.","se ficar tão perto dele, corre o risco de pegar essa doença de cabeça de vento."),
                ("I don't know where you're going, but I will escort you to your destination.","Não sei aonde você vai, mas faço questão de acompanhá-la."));
        }

        private IEnumerator PlayCherrlFaint(NPCController cherrl,NPCController ghosh,int version)
        {
            // Native actor command 0D:16 switches to the original seated/faint bank.
            RetailResidentBanks.ApplyBank(cherrl.gameObject,"cheril_recovery");
            cherrl.GetComponent<RetailTownResidentSpriteView>()?.PlayClip("idle");
            PlayAreaMusic("Town.mp3");
            yield return new WaitForSecondsRealtime(20f/60f); // original script wait 0x14
            if (version != _cherrlWalkVersion || !cherrl || !ghosh) yield break;
            _cherrlWalkAnimating=false;
            GhoshTalk(ghosh,"I didn't do anything!","Eu não fiz nada!",()=>
            { _cherrlWalkAnimating=true; StartCoroutine(CarryCherrlToClinic(cherrl,ghosh,version)); });
        }

        private IEnumerator CarryCherrlToClinic(NPCController cherrl,NPCController ghosh,int version)
        {
            yield return PlayStoryWalk(ghosh,RetailTownActorPosition(CherrlWalkGhoshExit,0f),2f);
            if (version != _cherrlWalkVersion || !cherrl || !_player) yield break;
            ghosh.gameObject.SetActive(false);
            TownRetailScriptFlags.CreatePersistent().SetBit(4069); // native F_gosh0000 (0xFE5)
            for(float t=0;t<.5f;t+=Time.unscaledDeltaTime) { _fadeToBlack=t/.5f; yield return null; }
            if (version != _cherrlWalkVersion || !cherrl || !_player) yield break;
            _fadeToBlack=1f;
            cherrl.enabled=false;
            _cherrlWalkCarrying=true;
            _player.transform.position=RetailTownActorPosition(CherrlWalkClinicApproach,TownActorFootOffset);
            _townFacing=Vector2.down;
            _kohSprite?.PresentLift(true);
            UpdateCherrlWalkCarry();
            UpdateCameraFollow(snap:true);
            _fadeToBlack=0f;
            var from=_player.transform.position;
            var destination=RetailTownActorPosition(CherrlWalkClinicDoor,TownActorFootOffset);
            for(float t=0;t<1.1f;t+=Time.unscaledDeltaTime)
            {
                if(version!=_cherrlWalkVersion || !_player || !cherrl)yield break;
                _player.transform.position=Vector3.Lerp(from,destination,Mathf.Clamp01(t/1.1f));
                _playerLocomotion?.Present(destination-from,t*4f,true);
                UpdateCherrlWalkCarry();
                yield return null;
            }
            if(version!=_cherrlWalkVersion || !_player)yield break;
            _player.transform.position=destination;
            _cherrlWalkTransitioning=true;
            yield return LoadInterior(CherrlHospital);
            _cherrlWalkTransitioning=false;
            if(version!=_cherrlWalkVersion || StoryRoom!=CherrlHospital) { ResetCherrlWalk(); yield break; }
            TownSaveVariables.SetBool(CherrlWalkCompletedKey,true);
            PersistTownProgress();
            ResetCherrlWalk();
            var hush=FindStoryActor("hush");
            if(hush)CherrlWalkAssessment(hush);
        }

        private bool CherrlWalkAssessment(NPCController hush) =>
            TownTalkSequence(hush,()=>AdvanceTownStory("cherrl",CherrlHerbAvailable),
                new TownStoryLine("Don't worry, it's nothing serious.\nEverything will be okay.",
                    "Não se preocupe, não é grave.\nVai ficar tudo bem."));

        private bool CherrlWalkApology(NPCController cherrl) =>
            CherrlTalkSequence(cherrl,()=>CherrlRestConversation(cherrl,()=>NadiaAsk(cherrl,
                "It is not unusual for Cherrl to faint.\nDo not feel guilty about it.",
                "A Cherrl desmaia às vezes.\nNão se sinta culpado.",
                (T("Continue","Continuar"),()=>
                { TownSaveVariables.SetBool(CherrlWalkApologyKey,true); PersistTownProgress(); }))),
                new CherrlStoryLine(0,"... don't worry about it.","... não se preocupe."),
                new CherrlStoryLine(0,"I'm really sorry, {name}.\nI'm always causing trouble for you.",
                    "Sinto muito, {name}.\nEstou sempre dando trabalho para você."),
                new CherrlStoryLine(0,"But I'm still very happy.\n... even if it was for a short while, my dream did come true.",
                    "Mas estou muito feliz.\n... mesmo que tenha sido por pouco tempo, meu sonho se realizou."));
        private void UpdateCherrlWalkCarry()
        {
            if(!_cherrlWalkCarrying || !_cherrlWalkCompanion || !_player)return;
            _cherrlWalkCompanion.transform.position=_kohSprite ? _kohSprite.LiftHandPosition(_gameCamera,_townFacing) :
                _player.transform.position+Vector3.up*.75f;
        }

        private void ResetCherrlWalk()
        {
            if(_cherrlWalkTransitioning)return;
            _cherrlWalkVersion++;
            _cherrlWalkActive=_cherrlWalkAnimating=_cherrlWalkCarrying=false;
            _cherrlWalkCompanion=null;
            _fadeToBlack=0f;
            _kohSprite?.PresentLift(false);
        }
    }
}
