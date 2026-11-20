# adsnd — música original renderizada a partir do disco

A trilha do Azure Dreams (PS1) não existe como áudio no CD. Cada música é uma sequência **KDT** da Konami
(contêiner `KCET → TSDB → KDT2 → "KDT "`), tocada em tempo real pelo driver de som do jogo no SPU, com os
instrumentos de um banco **VAB** da Sony. As faixas do disco em `STR/` são só vozes e vinhetas.

`adsnd.c` reimplementa o driver a partir do decomp (`azure-dreams-decomp-master/src/engine/audio/sequence`)
e um SPU (ADPCM com flags de loop, ADSR de hardware, afinação, volume/pan, envio de reverb).

## Fatos que valem lembrar

- **Bancos:** todas as músicas da cidade usam o VAB de `TOWN.BIN` setor 1245; as da torre, o VAB de
  `DUNGEON.BIN` setor 3780 (descoberto pelos programas que cada sequência usa).
- **No disco, o corpo do VAB (samples) começa no setor seguinte ao cabeçalho**, não logo depois dele.
- **Tempo:** o sequenciador avança 1 tick por interrupção do root counter 2 (`SetRCnt(2, 0x1C00)`,
  33,8688 MHz ÷ 8 ÷ 0x1C00 = **590,625 Hz**); modulação a cada 11 ticks. Confirmado contra a OST (1,00×).
- **Formato KDT:** cabeçalho `"KDT "`, tamanho, divisão (480), nº de trilhas; tamanhos das trilhas (u16) em
  0x10; dados a partir de 0x50. Eventos compactos (`audio_read_compact_sequence_event`); bit 7 do segundo
  byte = "sem delta a seguir". Loop: cc99=20 salva, cc99=30 volta (contagem via NRPN 20 + cc6; 127 = infinito).
- Um contêiner pode ter **duas** sequências (ex.: Entrance/Exit of Comedians); `render_all.py` varre tudo.

## Uso (na raiz do workspace `Projetos Pessoais/Azure`)

```bash
gcc -O2 -o adsnd "Azure Dreams Reimagined/Tools/adsnd/adsnd.c" -lm
python3 "Azure Dreams Reimagined/Tools/adsnd/render_all.py" ./adsnd /tmp/render 2 8      # 57 sequências
python3 "Azure Dreams Reimagined/Tools/adsnd/match_ost.py" /tmp/render <gravações...>        # opcional: valida
python3 "Azure Dreams Reimagined/Tools/adsnd/export_classic.py" /tmp/render                 # gera o Clássico
```

`export_classic.py` contém o mapeamento cue → sequência e grava `Media/Music/<cue>.ogg` (um ciclo exato,
com crossfade de 46 ms na emenda, −16 LUFS) e `Resources/Music/LevelUp.ogg`.
Todos os renders, inclusive variações ainda não usadas pelo jogo (tema da cidade por nível 1–5, casa do
Koh por reforma, Pub/Vivian, Temple 1/2), ficam em `../extracted_assets/music_rendered/`.
