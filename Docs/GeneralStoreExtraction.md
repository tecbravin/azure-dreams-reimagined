# General Store — itens originais extraídos

Reprodução: `Tools/extract_general_store_items.py` (Python + Pillow).

Destino: `Assets/AzureDreams/Resources/Town/GeneralStore/`.

- `catalog.json`: 31 mercadorias da tabela de móveis/equipamentos, o serviço Remove e a Music Box do inventário: 33 registros ao todo.
- 33 PNGs transparentes de 16×16, associados por ID estável. O original compartilha cinco descritores de ícones entre os registros. A casa nos móveis é o ícone original, não uma miniatura de cada móvel.
- Textos em inglês decodificados diretamente do Shift-JIS e normalizados de largura completa para Unicode, preservando frases, quebras e erros gramaticais do disco.
- Registros de 20 bytes em TOWN.BIN `0x573E4 + índice * 20`: ícone no halfword +2, ponteiros de nome e descrição em +4/+8, preço em +16. O delta de carregamento é `0x8007D760`.
- Ícones: tabela `SLUS_006.14:0x8007361C`, descritores nativos e VRAM original da general store. Os hashes autenticados dos dois binários são verificados antes de extrair.
- Music Box: registro de inventário `SLUS_006.14:0x45BFC`; nome `0x8002F738`, descrição `0x8002F6DC`, ícone 20. Seu preço de 700 G vem do estoque existente e não foi inferido desse registro.

Validação: todos os ponteiros de texto, o percentual dos registros e o formato/tamanho dos sprites foram conferidos; nenhum PNG vazio. A folha `Validation/GeneralStore/original-icons.png` foi inspecionada. Estes arquivos são a extração dos textos e ícones de menu; não substituem os modelos 3D dos móveis nem alteram a interface ou a compra existentes.
