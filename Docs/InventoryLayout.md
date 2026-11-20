# Layout do inventário

O grupo de painéis de itens, status e descrição é centralizado horizontal e verticalmente. As colunas cresceram de 228 para 299 unidades na tela lógica de 640, chegando a 445 em telas largas. A altura principal chega a 300, ajustando-se à faixa de familiares sem ultrapassar a tela. A descrição tem 112 unidades de altura e aceita até quatro linhas. Os talentos ficam na área de descrição, sem uma janela adicional fora do conjunto.

Nomes, cabeçalhos, campos numéricos e botões destes painéis usam ajuste de fonte sem quebra automática. O texto descritivo tem seu próprio estilo com quebra e limite de quatro linhas. A opção Ordenar tem espaço reservado para não cobrir o título. As linhas de itens cresceram conforme a altura do painel.

Validação: compilação do runtime e teste isolado em Unity para larguras lógicas 640, 854 e 1066, com zero a cinco familiares e a página de familiares; centralização, ausência de sobreposição e cortes fora da tela, dimensões maiores e ajuste de nomes longos com a fonte OpenDyslexic real. Recibo: Validation/NpcRestaurant/InventoryLayout-passed.txt. A interface completa não foi capturada visualmente nesta execução.
