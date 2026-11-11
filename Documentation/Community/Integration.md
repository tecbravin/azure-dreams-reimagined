# Integração e cruzamento das fontes

## Pastas no projeto

| Tipo | Pasta sob Assets/AzureDreams |
|---|---|
| Quadros de monstros | Sprites/Monsters/Community/monster_XX/animation_YY |
| Sheets e personagens | Sprites/Monsters/Community/Sheets e Sprites/Characters/*/Community |
| Retratos | Sprites/Portraits/ADStateDisplay |
| Ícones de itens | Sprites/Items/ADStateDisplay |
| Ícones de HUD/save/elementos | Sprites/UI/ADStateDisplay |
| Fontes | Fonts/Community/ADStateDisplay |
| Construções e Town mínima/máxima | Models/Town/CapturedScenes |
| Texturas e máscaras STP | Textures/Town/CapturedScenes |
| Referências da comunidade | Data/Community |
| Textos de itens do jogo | Data/Items/Retail |
| Referência de itens | Data/Items/Community |
| Créditos do jogo | Data/Credits/Retail |
| Grades e metadados de capturas | Data/Town/CapturedScenes |

VRAM bruta das 44 capturas fica em Assets/StreamingAssets/Extracted/CapturedScenes.
Documentos PDF/ODS/MHTML originais foram preservados em Documentation/Community/Sources.
As capturas .3dm originais permanecem na pasta AzureDreams_3D_models do workspace.

## Cruzamentos efetuados

- integration_manifest.json: 8.457 entradas com fonte, asset canônico e hash.
  Imagens idênticas em bytes reutilizam o asset existente, mesmo que tenham
  nomes diferentes. A relação fonte->asset mantém cada quadro identificável.
- building_crosswalk.json: 16 pontuações, bytes dos respectivos registros da
  tabela autenticada em TOWN.BIN e 15 registros com candidatos de captura 3D
  por nome. Bytes comprovam o conteúdo do registro; não comprovam que o ID da
  planilha e o ID do nome da captura pertençam ao mesmo domínio semântico.
- item_text_crosswalk.json: 146 frases curtas do pool autenticado encontradas
  no guia de itens. Correspondência lexical pode ocorrer em descrições e não
  deve ser tratada como uma identificação definitiva do item.
- town_event_reference.json: 139 células de eventos com linha, coluna, rótulo
  de dia, local e rota da planilha, para validar pré-requisitos e diálogos.
- sprite_inventory.json: 4.419 imagens, 1.191 grupos de animação contíguos e 46
  IDs de monstro. Corrige a contagem anterior que ignorava IDs hexadecimais
  com letras. Monster ID é hexadecimal; animação e quadro são decimais.

O inventário e os metadados preservam IDs, texto original e hashes. Identidade
dos monstros, FPS e significado das animações continuam não confirmados. Não
foram atribuídos prefabs de NPCs ou eventos a partir desses números.

## Importação e verificação

CommunityAssetImporter aplica filtro pontual, sem mipmaps e sem compressão;
imagens de personagens/ícones entram como Sprite Single, sem adivinhar cortes
de spritesheets. Máscaras STP usam espaço linear. Os OBJ usam normais calculadas
quando necessário. Os MTL foram ajustados à localização final das texturas.

Logs/CommunityAssetImport.json registra a validação nativa Unity de modelos,
texturas, fontes e existência dos arquivos. Tools/integrate_community_assets.py
reproduz a organização e verifica hashes e caminhos de materiais.

Os modelos capturados e as referências estão disponíveis como assets. Esta
integração não substitui automaticamente objetos da cena, implementa quests
nem converte alegações comunitárias em regras de runtime. Semitransparência
PS1 e variantes de faces exigem tratamento no renderizador do port.
