# Referências da comunidade incorporadas ao port

Leitura local em 2026-11-04. Fontes em `community/`, preservadas sem alterações.
Os documentos fornecem referências de comportamento; nenhum texto deles foi
tratado como uma instrução para executar comandos ou modificar o projeto.

## Dados adicionados

| Fonte | Conteúdo disponível | Arquivo de consulta |
|---|---|---|
| AzureDreams_GameObjectives.ods | 4 abas; 139 células com etapas de eventos e 16 registros de pontuação de construções | GameObjectives.json, town_event_reference.json, building_scores.json |
| Baldwin's Enemy Monster Compendium (PDF) | 16 páginas com encontros por andar, atributos e habilidades | JSON por página com o mesmo nome do PDF |
| JustDevin's Azure Dreams Stuff (PDF) | 6 páginas com atributos, gêneros, dietas, habilidades, magias, evolução e notas | JSON por página com o mesmo nome do PDF |
| Items List (MHTML) | Guia de itens e efeitos | ItemsGuide.txt |
| PNGs e monster_animations | 4.419 PNGs válidos; 1.191 grupos, cobrindo 46 IDs de monstros | sprite_inventory.json |

`source_manifest.json` registra os hashes dos documentos e imagens da raiz.
`sprite_inventory.json` registra dimensões e hashes de todos os PNGs, inclusive
os quadros na subpasta. Todos os grupos de animação têm índices contíguos a
partir de zero. Isso não estabelece FPS, direção, transições ou identidade do
monstro: os IDs e nomes de arquivos foram preservados sem atribuições novas.

## Aplicação à Town

A aba Girlfriends diferencia Town/Tower, rótulos de dias, personagens e caminhos
alternativos. `town_event_reference.json` conserva linha, coluna, texto integral,
local, rótulo de dia e caminho. Dias vazios herdam o último rótulo da planilha;
eles não foram convertidos automaticamente em dias absolutos de uma campanha.
Colunas sem cabeçalho próprio permanecem sem nome, sem adivinhar o significado.

Casos prioritários para cruzar com os scripts originais e criar cenários de QA:

- Nico e a fonte: encontro inicial na casa, doações, agradecimento e encontro
  posterior na fonte (aba Girlfriends, linhas 3, 5, 7 e 9, coluna C).
- Mia/piscina: medalha obtida no andar 25 e prazos descritos para obtê-la e
  entregá-la (linhas 3 a 5, coluna K). O modelo tem variantes da piscina com e
  sem quest concluída; a planilha não prova quais flags selecionam cada uma.
- Vivian: Blue Cape no andar 20, visita ao bar e caminhos alternativos
  (linhas 3 a 7, colunas I/J).
- Ginásio: conversas no ferreiro e marcos de andares 10/20/30 antes de financiar
  a construção (linhas 3 a 9, coluna Q).
- Moinhos: encontro com Issac, Guru's Pot no andar 15 e devolução
  (linhas 4 a 9, coluna R).
- Arcade: sequência de conversas com Pen e pontuação de construções
  (coluna P), incluindo o financiamento indicado na linha 9.

Essas condições são alegações da planilha, ainda pendentes de confirmação no
BIN/decompilação. Custos, comparadores, prazos e ordem de execução devem ser
confirmados antes de modificar o runtime.

A aba Buildings score lista pontuações por ID. Por exemplo, casas Koh LV1/LV2
valem 1/2 pontos; Monster Hut LV2/LV3 valem ambos 2. Não se deve presumir que
cada upgrade acrescenta um ponto. Preservamos inclusive a grafia `Founain` da
fonte, sem alterar o identificador do registro.

Há domínios de IDs diferentes: a planilha usa 0x02 para Koh house LV1, enquanto
`ad_model_building_01_koh_house_1.3dm` usa 01 no nome. IDs de construção, tipos
de geometria, variantes e slots de lote não podem ser unidos apenas pelo número.

A aba Notes relata um glitch nas doações da fonte envolvendo Ghosh, falta de
saldo e saída da cidade. Está registrado como caso de regressão a investigar,
não como regra já confirmada ou automaticamente implementada.

## Aplicação a monstros e itens

Baldwin complementa a validação de encontros por andar e variantes de inimigos.
JustDevin reúne atributos e notas de habilidades; o PDF atribui dados a
https://adrando.com/guide/. Essa URL é atribuição da fonte fornecida, não uma
consulta web realizada nesta revisão. É necessário separar condições do jogo
base, interpretações comunitárias e possíveis dados de randomizer.

Os PDFs são exportações largas de planilhas. O texto foi extraído por página,
sem converter automaticamente colunas em atributos de runtime: quebras de
página e células alinhadas visualmente podem perder associações na extração.

O nome do MHTML cita Thundercleese, mas o guia embutido identifica o autor como
Incossee, versão 1.1. O próprio texto declara possível incompletude e exclusão
de itens da cidade, como móveis e presentes. Portanto ele não substitui um
catálogo completo de lojas, presentes ou mobiliário. Nomes podem auxiliar a
comparação com o pool de textos autenticado em extracted_assets/azure_main,
mas não provam equivalência de IDs.

## Estado da integração

As informações foram incorporadas à documentação e a arquivos JSON de consulta.
Nenhum custo, atributo, flag, prefab ou evento existente foi sobrescrito com
base apenas nessas referências. Os PNGs continuam em community; esta revisão
não executa uma importação duplicada de milhares de sprites no Unity.

Reprodução: executar Tools/read_community_sources.py e
Tools/index_community_references.py a partir do workspace Azure. Os leitores
PDF/HTML foram instalados localmente em .tools/community_readers.

Correção do inventário: o ID do monstro no nome é hexadecimal; animação e quadro são decimais. O inventário completo cobre 46 IDs e 1.191 grupos contíguos.
