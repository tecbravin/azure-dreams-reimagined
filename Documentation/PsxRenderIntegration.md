# Pendências recuperadas dos bots — integração de renderização

## Escopo recuperado

Fontes: histórico Art Director (`local-96a790ab5d1f2f24cc7c52ea7c0adf9756477bcc5fe59604c26dc178d991c297`, mensagens 453–504) e Game Tester (`local-30c51c8c4cb8582c2709f5a163ab14ea3a0a560c9fd76419100ed28857df40a2`). Os históricos são requisitos e relatos, não provas de execução.

| Pedido/problema | Implementação | Estado de validação |
| --- | --- | --- |
| Shader Built-in indexado 4 bpp + CLUT original | `Assets/AzureDreams/Shaders/PsxIndexed.shader` | Código gravado; validação GPU/Unity pendente |
| RGB555, word zero transparente, preto `0x8000`, STP condicionado à primitiva | `ReadPixel`, passes `OPAQUE_TEXELS`/`STP_TEXELS` | Testes sob responsabilidade do Game Tester |
| ABR Half/Add/Subtract/AddQuarter | `PsxIndexedMaterial.Configure/ApplyBlendState` e inspector | Equações de blend implementadas; não alegar equivalência bit a bit ao GPU PS1 |
| Materialização dos bancos validados | `Azure Dreams > Rendering > Import Verified Indexed Sprites` | Importador implementado; executar e consultar `Logs/PsxShaders/import-report.json` |
| Índices/CLUT sem sRGB, filtro ou compressão destrutivos | `PsxDataTextureImporter`, `CreateClutTexture` | Configuração explícita, inclui override WebGL RGBA32 |
| Postprocess usava URP ausente e propriedades inexistentes | `PostProcessingController`, `BuiltinPostProcess.shader`, `BuiltinPostProcessProfile` | Substituído por Built-in real; precisa execução GPU |
| Prefab/profile de postprocess tinham GUIDs e tipos de Volume inválidos | `PsxShaderIntegration.CreatePostProcessingAssets` | Recria assets tipados ao executar o importador, com cópia do stub antigo em Logs |
| AudioImporter.spatialize inexistente | `Assets/Editor/AudioImportPostprocessor.cs` | Removido; spatialization pertence ao AudioSource |
| AudioImporter alterava uma cópia de defaultSampleSettings e não aplicava | Atribuição de `importer.defaultSampleSettings = settings` | Corrigido no código |
| Asmdefs de testes não podem referenciar Assembly-CSharp | `AzureDreams.Runtime.asmdef`, `.asmref` no segundo root, `AzureDreams.Editor.asmdef` | Runtime chegou a gerar DLL no Editor; testes/runner mantidos pelo Game Tester |
| Animation/Audio/IMGUI removidos indevidamente do manifest | Módulos restaurados após confirmar que existem no Editor instalado | Sem apagar Library nem packages-lock |

## Contrato dos materiais

- Atlas `atlas_indices.png`: byte de índice 0–15 no canal R, normalizado por 255. Não é uma textura albedo.
- CLUT `clut_words.asset`: Texture2D 16×16 RGBA32 linear. R = byte baixo, G = byte alto do halfword original. Linha = paleta, coluna = índice.
- A transparência depende do **word de cor**, não do índice. Índice 0 pode ter uma cor válida. Apenas word `0x0000` é descartado.
- `0x8000` é preto com STP e não deve desaparecer.
- STP só habilita blend quando `_SemiTransparency` da primitiva está ligado.
- `Configure` e o inspector sincronizam os estados fixos de blend. Alterar apenas `_AbrMode` não basta.
- `SetPrimitiveColor` considera 0x80 como modulação neutra; comandos raw-texture ignoram a modulação.
- Cada banco recebe um material opaco e quatro materiais ABR, inicialmente na paleta 0. O consumidor deve selecionar a paleta original de cada parte e preservar a ordem de primitivas. Esses materiais não substituem por adivinhação os materiais dos atores existentes.
- A API permite ajustar depth test/write e filas. Isso não implementa automaticamente a ordering table PS1.
- Blend em color space Linear não reproduz a aritmética gamma do PS1. Mesmo em Gamma, quantização, dithering e arredondamento do framebuffer PS1 ainda não são uma promessa de equivalência bit a bit.

## Postprocess Built-in

O controller deve estar na **Camera** que renderiza a cena. O antigo Volume global não funcionava no pipeline deste projeto. O importador reconstrói `PostProcessingVolume.prefab` como um prefab de câmera com controller e preset reais; não o instancia nem salva a cena aberta.

Efeitos: FXAA, bloom local, ajustes de cor, vinheta, AO simplificado baseado apenas em profundidade, DOF, deslocamento cromático e grain. SMAA serializado antigo usa fallback FXAA (não é SMAA). MSAA depende também de Quality Settings. Estes efeitos são opcionais e não são requisitos do visual PS1 fiel; desligue-os para comparação com a extração original.

## Evidência já executada

`Logs/PsxShaders/implementation-status.json` registra a compilação diagnóstica com o Roslyn instalado junto ao Unity: código runtime e seis helpers Editor de implementação terminaram com **exit 0**. Os outputs foram redirecionados para Hermes scratch, sem substituir DLLs do Editor aberto. O runner novo do Game Tester foi adicionado depois da lista de fontes desse diagnóstico e não está coberto por ele.

Ainda não há prova de importação dos materiais, captura GPU, testes aprovados ou build WebGL. Não tratar a compilação C# como validação dos shaders.

## Compilação diagnóstica do grafo atual

`Logs/PsxShaders/current-csharp-graph-verification.json` registra uma rodada posterior que compilou os fontes atuais das quatro assemblies: `AzureDreams.Runtime`, `AzureDreams.Editor` (incluindo runner e showcase), `AzureDreams.Reimagined.EditorTests` e `AzureDreams.Reimagined.PlayModeTests`. Todas terminaram com exit 0. Foram usados os compiladores e as referências instaladas junto ao Unity, com DLLs/resultados redirecionados para Hermes scratch e sem atualizar as DLLs do Editor aberto.

Esse diagnóstico comprova compilação C#, não a resolução/reimportação do projeto pelo Editor nem aprovação dos testes. Os fixtures GPU/importação ainda exigem correções de expectativas, amostragem e PNGs válidos sob responsabilidade do Game Tester.

## Builder da demonstração

`Assets/Editor/PsxIndexedShowcaseBuilder.cs` passou na compilação diagnóstica após a revisão final do Coder. A prova está em `Logs/PsxShaders/showcase-parent-verification.json`, incluindo hash do fonte, exit code, limites de enquadramento calculados e confirmação de STP na paleta 1 do recurso 1.

A captura usa isolamento de cena por culling mask temporário (sem renderizar os objetos da cena do usuário), restaura o estado e preserva GUIDs dos materiais de demonstração em reexecuções. Isso é verificação de código/estrutura: ainda não há cena salva, captura GPU ou import-report confirmado. O enquadramento dos glifos reais e o resultado visual precisam ser observados após execução.

## Reprodução e evidências

1. Deixar o Editor importar/compilar naturalmente (não abrir outra instância no mesmo projeto).
2. Executar `Azure Dreams > Rendering > Import Verified Indexed Sprites`, ou criar o marcador `Library/PsxShaderImport.request` para a ponte Editor já carregada.
3. Inspecionar `Logs/PsxShaders/import-report.json`: status, contagens, mensagens de compilação, dispositivo gráfico e caminho da captura GPU.
4. Inspecionar visualmente `Logs/PsxShaders/resource_001_gpu.png` e comparar com a extração original.
5. Executar os testes GPU/Editor do Game Tester; resultados devem ser arquivos reais, não apenas inspeção de substrings.

A ponte importa somente os recursos listados em `../../extracted_assets/verified_decode/sprites/manifest.json`. Não importa a quarentena. `../../extracted_assets/verified_decode/README.md` documenta offsets, CLUTs, loaders e as correções da engenharia reversa.

## Fora da validação atual

- Os 11 KCET/TSDB/KDT2 foram classificados como contêineres de sequências de áudio, não texturas: `../../extracted_assets/verified_decode/kcet_report.md`. A verificação independente dos 20 registros passou com os comprimentos das pistas e limites dos contêineres.
- O recurso geométrico proprietário tipo 0x29 foi localizado e descomprimido a partir de DUNGEON offset 0x7E4000: `../../extracted_assets/verified_decode/geometry_report.md`. O payload bruto e a verificação reproduzível estão em `report_verification/parent_verified_report.json` e `report_verification/floor_tileset_type29_decoded.bin` sob verified_decode. Não há ainda exportação de mesh, vinculação das texturas nem renderização do piso.
- Os antigos PNGs grayscale 128×192 e supostos TMDs de 512 vértices não são assets validados.
- Texturas de chão/parede, geometria original, IDs de recursos em quarentena e seleção/animação de partes no runtime continuam exigindo investigação separada.
- Não foi executado build WebGL nem verificação no navegador. Configurações compatíveis com WebGL não equivalem a um build validado.
