# Acesso e circulação na Fortune House

Shiela passa a usar o alcance de conversa através do balcão (3 unidades), como os outros comerciantes. A seleção de interlocutor não faz raycast contra a bola decorativa, portanto ela não impede a conversa. A bola continua fixa, fora do inventário e sem interação de levantar.

O piso original foi conferido na geometria: piso principal Y2, escadas de madeira em X11–13 e X19–21/Z12–14 ligando Y2 a Y4, tablado superior em Y4. As escadas tinham sido incluídas nos retângulos sólidos e as faces inclinadas eram ignoradas. Agora as alturas são interpoladas nos triângulos originais; os pisos horizontais do tablado também são considerados. Telhado, ornamentos altos e vasos não se tornam caminháveis por essa alteração.

As duas passagens laterais de pedra (X9–10 e X22–23/Z15–17) ficam bloqueadas, inclusive durante saltos. O limite frontal termina no prédio (Z17), preservando o acesso à porta central em (16,16.5), às plantas e às paredes.

Validação: compilação e teste Unity isolado com o movimento real de Koh passaram: subida/descida nas duas escadas, travessia do tablado Y4, bloqueio das escadas externas, preservação das plantas/paredes/porta e conversa com Shiela a 1,8 unidade através do balcão com a bola presente. Recibo: `Validation/NpcRestaurant/fortune-access-passed.txt`. A antiga validação que autorizava as escadas laterais de pedra não representa mais o comportamento solicitado.
