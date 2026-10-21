"""Tabela de escala do jogo (aprovada em 20/10/2026). 1 unidade = 1 quadrado da grade = 64 px.

Os scripts de recorte usam isto para gerar cada desenho já no tamanho certo.
"""
PX = 64                     # pixels por quadrado

# Alturas de referência (quadrados).
PERSONAGEM = 1.65           # jogador e NPC adulto
ARVORE = 3.2                # árvores: 3 a 4
ARBUSTO = 1.0

# Prédios da cidade: largura em quadrados (a altura segue a proporção do desenho).
LARGURA_PREDIOS = {
    # do tamanho da casa
    "loja": 6, "mercearia": 6, "cavalaria": 6, "estabulo": 6, "taberna": 6, "hotel": 6,
    "boticario": 6, "armaria": 6, "ferraria": 6, "banco": 6, "hospital": 6, "arquearia": 6,
    "salao_musica": 6, "capela_simples": 5, "capela": 6, "guilda_ladroes": 6,
    # grandes
    "palacio_real": 12, "catedral": 10, "igreja_grande": 10, "mansao_real": 9, "igreja": 7,
    "salao_concerto": 6, "guilda_guerreiros": 6, "portao_real": 6,
    # estreitos e altos
    "torre_magica": 5, "torre_arcana": 5, "coreto_real": 5,
}


def largura_px(nome, padrao=6):
    return round(LARGURA_PREDIOS.get(nome, padrao) * PX)
