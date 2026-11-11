using UnityEditor;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined.Editor
{
    public sealed class PsxIndexedShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            base.OnGUI(editor, properties);
            if (EditorGUI.EndChangeCheck())
                foreach (Object target in editor.targets)
                    PsxIndexedMaterial.ApplyBlendState((Material)target);
            EditorGUILayout.HelpBox("Indices and CLUTs are linear data: Point, no mipmaps, no compression. " +
                "Only color-word zero is transparent. STP texels blend only when the primitive enables semi-transparency. " +
                "ABR subtract is background minus foreground. Match primitive order with sorting/queues.", MessageType.Info);
        }

        public override void ValidateMaterial(Material material)
        {
            PsxIndexedMaterial.ApplyBlendState(material);
        }
    }
}
