# Retratos originais do Beldo

Extração: `Tools/extract_beldo_portraits.py`.

Arquivos: `Assets/StreamingAssets/Media/Portraits/Dialogue/Beldo_variant0_frame0.png`, `Beldo_variant1_frame0.png`, `Beldo_variant2_frame0.png`.

Proveniência NTSC-U:

- Interlocutor 0x11, índice 13 da tabela em SLUS_006.14:0x8006B1EC.
- Tabela de variantes 0x8006B0C8, três registros de 8 bytes, antes da tabela do Guy em 0x8006B0E0.
- Animações 0x80074050, 0x80074060, 0x80074040; cada uma tem um quadro de dois ticks e controle final apontando para a própria animação.
- Pacote 0x80080EF8, LBA 0x2BCD, três setores; TOWN.BIN offset 0xFE4000. Duas imagens comprimidas são carregadas nas regiões originais da VRAM; paletas vêm dos recursos compartilhados originais.
- Nomes e quadros não estavam na lista fixa de 15 interlocutores do AdResourcesDumper, embora Beldo conste na tabela original.

Os hashes autenticados de TOWN.BIN e SLUS_006.14 são verificados. O extrator valida todos os acessos à VRAM e produz a montagem com paletas, espelhamentos e ordem original das partes. A folha de contato foi inspecionada visualmente. Arquivos Reimagined não são alterados. Esta mudança extrai os retratos; não muda as falas nem escolhe automaticamente qual variante usar em cada diálogo.

Metadados completos: `Assets/AzureDreams/Data/Extracted/BeldoPortraits/manifest.json`.
