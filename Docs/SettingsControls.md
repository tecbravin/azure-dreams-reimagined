# Configurações: rolagem e tecla de voltar

Configurações tem barra vertical de rolagem, inclusive nas listas de controles e efeitos gráficos. A seleção por teclado acompanha a rolagem no painel principal.

Os rótulos Voltar/Cancelar/Fechar usam WithCancelHint, que lê GetBindingDisplayString da ação Cancel. Q continua padrão. A ação agora aparece em Mapeamento de controles como Voltar / cancelar / pular, com preferência persistente e restauração de padrões. O menu inicial, Configurações, telas da casa e checkpoints passam a consultar a ação Cancel em vez de verificar Q diretamente. Q deixa de ser reservado, mas a prevenção de teclas duplicadas continua ativa.

Compilação do runtime validada. Os botões dos controllers não contêm mais [Esc] ou [Q] fixos.


Padrões atualizados: inventário I; Configurações O; virar sem andar C; mapa M; girar câmera à esquerda K e à direita L; levantar X. A ordem seguir/esperar do familiar passa de K para J para evitar conflito. A cena principal e os valores iniciais dos scripts foram atualizados; preferências pessoais existentes continuam válidas. O continua reservado para Configurações.

Os títulos Configurações, Mapeamento de controles e Efeitos gráficos ficam centralizados. As descrições de estilo e correr automático aparecem em tooltips; o rótulo e o botão de correr automático ocupam uma única linha. Os sliders reservam 76 unidades para a porcentagem, sem quebra. Botões de vínculo no mapeamento passam de 125 para 162,5 de largura e recebem 31,2 de altura (base 24 × 1,3); os botões Restaurar e Voltar permanecem com o tamanho anterior.
