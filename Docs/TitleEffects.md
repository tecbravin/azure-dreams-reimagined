# Abertura: lens flare, cristais e sons

Assets originais extraídos de MAIN.BIN, pacote de textura 0x257000: dez elementos de lens flare e duas sequências de sete quadros dos cristais, com paletas CLUT e quatro ticks por quadro. O extrator reproduzível é `Tools/extract_title_effects.py`. Não são formas geradas nem o cristal genérico do inventário.

Evidência de código original em azure-dreams-decomp-master:
- `src/frontend/settings/create_settings_controller.c`: cria os dois cristais, sequências 0x8008B270 e 0x8008B230, e lens flare com ângulo inicial 0xE00.
- `update_settings_effect.c`: dez reflexos, posição elíptica em torno de (160,120), ângulo -12 por tick, subida de brilho e desaparecimento aos 160 ticks. As tabelas em 0x80088790, 0x800887B8 e 0x800887CC fornecem sprites, distâncias e máscaras de cor. O runtime usa essas tabelas e composição aditiva, sem brilho persistente.
- `update_settings_phase_motion.c`: ao confirmar, velocidades horizontais +/-2, aceleração por fator 1,125 e velocidade vertical -4. Os cristais cruzam a tela enquanto continuam girando.
- `update_settings_menu_animation.c`: 105 ticks entre fim da subida e chegada do menu, confirmação com 96 ticks, piscar da linha selecionada e som **0x51A para Continuar e Novo Jogo**, **0x522 para Opções**. Ambos os casos iniciais usam o mesmo som no original; 0x503/0x505 eram substituições incorretas.

`Tools/extract_retail_sfx.py --files ../extracted_assets/full_disc/files 51A 522` decodifica os comandos do VAB original para WAV, já ligados e pré-carregados. O som 51A dura aproximadamente 1,546 s. As proteções de save e confirmação de sobrescrita continuam ativas.

Validação: runtime compila; teste isolado Unity verifica assets, shader, seletores de som, confirmação de ambos os casos após 96 ticks, bloqueio de clique repetido e de Continuar sem save, trajetória espelhada. Renderização GPU verificada nos ticks 90, 138 e 170: brilho e reflexos em movimento e desaparecimento. Capturas em Validation/NpcRestaurant/Title-lens-*.png; recibo TitleEffects-passed.txt.
