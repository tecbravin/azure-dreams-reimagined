# Estabilidade da janela externa da Cherrl

A personagem da primeira visita estava ancorada junto ao plano da fachada. O sprite conserva a orientação de billboard, mas a profundidade padrão também girava com a câmera, cruzando a parede em ângulos oblíquos. Isso produzia cortes e aparente flickering.

A configuração externa da Cherrl agora acrescenta 0,04 unidade para fora da fachada e informa a normal fixa da parede ao `ActorSpriteDepth`. O shader intersecta a profundidade com esse plano, preservando a silhueta voltada à câmera. A normal é uma propriedade por personagem; zero mantém o comportamento anterior dos demais atores. Retratos, texturas, tamanho, interações e colliders não foram alterados.

Validação: runtime compilado. Teste GPU isolado reproduziu o corte antigo em 24 configurações e preservou a silhueta após a correção em 30 combinações de ângulo, inclinação e projeção. Objetos à frente continuam ocultando o sprite. Recibo: `Validation/NpcRestaurant/cherrl-window-depth-passed.txt`. O teste reproduz a relação sprite/fachada; não é uma captura da cena completa da cidade.
