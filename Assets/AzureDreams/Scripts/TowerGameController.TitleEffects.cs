using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [Serializable] private sealed class RetailTitleEffects
        {
            public int[] offsets, masks, crystalTicks;
            public int confirmationTicks, initialAngle, angleStep;
        }
        private RetailTitleEffects _retailTitleEffects;
        private readonly Texture2D[] _retailTitleLens = new Texture2D[10];
        private Texture2D _retailTitleCrystals;
        private Material _retailTitleLensMaterial;
        private int _titleConfirmRow = -1;
        private float _titleConfirmStarted;

        private void LoadRetailTitleEffects()
        {
            if (_retailTitleEffects != null) return;
            var data = AssetMode.Load<TextAsset>("Title/RetailEffects/data");
            if (!data) return;
            _retailTitleEffects = JsonUtility.FromJson<RetailTitleEffects>(data.text);
            _retailTitleCrystals = AssetMode.Load<Texture2D>("Title/RetailEffects/Crystals");
            for (int i=0;i<10;i++) _retailTitleLens[i] = AssetMode.Load<Texture2D>("Title/RetailEffects/Flare"+i);
            var shader = AssetMode.Load<Shader>("Title/RetailEffects/TitleLens");
            if (shader) _retailTitleLensMaterial = new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
        }

        private void TickTitleConfirmation()
        {
            if (_titleConfirmRow < 0) return;
            LoadRetailTitleEffects();
            if ((Time.unscaledTime-_titleConfirmStarted)*60f < (_retailTitleEffects?.confirmationTicks ?? 96)) return;
            int row=_titleConfirmRow;
            _titleConfirmRow=-1;
            CompleteTitleOption(row);
        }

        internal static Vector2 TitleCrystalFlight(float ticks, bool left)
        {
            // update_settings_phase_motion: dx +/-2, dx += dx/8, dy=-4 per tick.
            float frames=Mathf.Clamp(ticks,0f,40f);
            return new Vector2((left ? 1f : -1f)*16f*(Mathf.Pow(1.125f,frames)-1f),-4f*frames);
        }

        private void DrawRetailTitleCrystals(Rect row, Rect band)
        {
            LoadRetailTitleEffects();
            if (!_retailTitleCrystals) return;
            float ticks=(Time.unscaledTime-_titleMainStarted)*60f;
            int duration=0; foreach(int t in _retailTitleEffects.crystalTicks)duration+=t;
            float phase=Mathf.Repeat(ticks,duration); int frame=0;
            while(frame+1<_retailTitleEffects.crystalTicks.Length && phase>=_retailTitleEffects.crystalTicks[frame])phase-=_retailTitleEffects.crystalTicks[frame++];
            float size=row.height*4f; // Each native 64px cell contains a 16px crystal.
            for(int side=0;side<2;side++)
            {
                bool left=side==0;
                Vector2 center=new(left ? band.x-row.height*.5f : band.xMax+row.height*.5f,row.center.y);
                if(_titleConfirmRow>=0)center+=TitleCrystalFlight((Time.unscaledTime-_titleConfirmStarted)*60f,left)*(row.height/16f);
                GUI.DrawTextureWithTexCoords(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),_retailTitleCrystals,
                    new Rect(frame/7f,left ? .5f : 0f,1f/7f,.5f));
            }
        }

        private void DrawRetailTitleLens(Rect screen)
        {
            if(Event.current.type!=EventType.Repaint)return;
            RenderRetailTitleLens(screen, GUI.matrix, (Time.unscaledTime-_titleMainStarted)*60f, Screen.width, Screen.height);
        }

        private void RenderRetailTitleLens(Rect screen, Matrix4x4 matrix, float ticks, int targetWidth, int targetHeight)
        {
            LoadRetailTitleEffects();
            if(_retailTitleEffects==null || !_retailTitleLensMaterial)return;
            if(ticks<0f || ticks>=160f || _titleConfirmRow>=0)return;
            float intensity=ticks<128f ? (64f+Mathf.Floor(ticks/4f))/255f : ticks<145f ? Mathf.Lerp(96f/255f,1f,1f-Mathf.Pow(.5f,ticks-128f)) : Mathf.Pow(.875f,ticks-145f);
            float angle=(_retailTitleEffects.initialAngle+_retailTitleEffects.angleStep*ticks)*2f*Mathf.PI/4096f;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f,targetWidth,targetHeight,0f);
            for(int i=9;i>=0;i--)
            {
                var texture=_retailTitleLens[i]; if(!texture)continue;
                float offset=_retailTitleEffects.offsets[i];
                Vector2 center=new((160f+Mathf.Cos(angle)*offset*.5f)*screen.width/320f,(120f+Mathf.Sin(angle)*offset*1.25f)*screen.height/240f);
                int mask=_retailTitleEffects.masks[i];
                Color color=new((mask&255)/255f,((mask>>8)&255)/255f,((mask>>16)&255)/255f,intensity);
                _retailTitleLensMaterial.mainTexture=texture;
                _retailTitleLensMaterial.SetPass(0);
                float rotation=i==0 ? angle : i==1 ? -angle : 0f;
                float cosine=Mathf.Cos(rotation),sine=Mathf.Sin(rotation);
                GL.Begin(GL.QUADS);GL.Color(color);
                Vertex(-32f,-32f,0f,1f);Vertex(32f,-32f,1f,1f);Vertex(32f,32f,1f,0f);Vertex(-32f,32f,0f,0f);
                GL.End();
                void Vertex(float x,float y,float u,float v)
                {
                    Vector3 point=new(center.x+(x*cosine-y*sine)*screen.width/320f,center.y+(x*sine+y*cosine)*screen.height/240f,0f);
                    GL.TexCoord2(u,v);GL.Vertex(matrix.MultiplyPoint3x4(point));
                }
            }
            GL.PopMatrix();
        }
    }
}
