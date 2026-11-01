"""
Genera lol_coach_data.json para el coach de League of Legends.

Fuentes:
  - Riot Data Dragon (oficial): campeones, objetos y hechizos de invocador en es_ES.
  - u.gg (endpoint público no oficial stats2.u.gg): orden de habilidades y builds
    más jugadas por campeón y rol, todos los rangos, región mundial.

Uso:  python generar_datos_lol.py [--version 16.21.1] [--salida lol_coach_data.json]
Ejecútalo en cada parche para refrescar los datos.
"""
import argparse
import json
import re
import sys
import time
import urllib.request

DDRAGON = "https://ddragon.leagueoflegends.com"
UGG = "https://stats2.u.gg/lol/1.5/overview/{patch}/ranked_solo_5x5/{champ_id}/1.5.0.json"

REGION_MUNDIAL = "12"
TIER_TODOS = "8"
TIER_RESPALDO = "10"  # platino+
ROLES = {"1": "jungla", "2": "support", "3": "adc", "4": "top", "5": "mid"}
MIN_PARTIDAS_ROL = 300
MIN_CUOTA_ROL = 0.10  # el rol debe suponer al menos el 10 % de las partidas del campeón

# Slot de la API (0-6) -> tecla por defecto en el juego
TECLA_POR_SLOT = {0: "1", 1: "2", 2: "3", 3: "5", 4: "6", 5: "7", 6: "4"}


def get_json(url, intentos=3):
    req = urllib.request.Request(url, headers={"User-Agent": "lol-coach-data/1.0"})
    for i in range(intentos):
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.loads(r.read().decode("utf-8"))
        except Exception as e:
            if i == intentos - 1:
                raise
            time.sleep(1.5 * (i + 1))


def limpiar_html(texto):
    texto = re.sub(r"<br\s*/?>", " ", texto)
    texto = re.sub(r"<[^>]+>", "", texto)
    return re.sub(r"\s+", " ", texto).strip()


def tipo_activo(item):
    tags = set(item.get("tags", []))
    nombre = item["name"].lower()
    if "Trinket" in tags:
        return "vision"
    if "Consumable" in tags:
        if "poción" in nombre or "galleta" in nombre or "frasco" in nombre or "HealthRegen" in tags or "ManaRegen" in tags:
            return "curacion"
        if "guardián" in nombre or "Vision" in tags:
            return "vision"
        return "consumible"
    if {"Active"} & tags or "<active>" in item.get("description", "").lower():
        if {"Armor", "SpellBlock", "Tenacity"} & tags:
            return "defensa"
        if {"NonbootsMovement"} & tags:
            return "escape"
        if {"Damage", "SpellDamage", "CriticalStrike", "AttackSpeed"} & tags:
            return "ofensivo"
        return "utilidad"
    return None


def cargar_ddragon(version):
    base = f"{DDRAGON}/cdn/{version}/data/es_ES"
    champs = get_json(f"{base}/champion.json")["data"]
    items = get_json(f"{base}/item.json")["data"]
    summ = get_json(f"{base}/summoner.json")["data"]
    return champs, items, summ


def nombre_item(items, item_id):
    it = items.get(str(item_id))
    return it["name"] if it else None


def lista_items(items, ids):
    vistos, out = set(), []
    for i in ids:
        n = nombre_item(items, i)
        if n and i not in vistos:
            vistos.add(i)
            out.append({"id": i, "nombre": n})
    return out


def mejor_opcion(items, opciones, excluir):
    """opciones: [[item_id, wins, matches], ...] -> el más jugado no repetido."""
    for op in sorted(opciones, key=lambda x: -x[2]):
        if op[0] not in excluir and nombre_item(items, op[0]):
            return op[0]
    return None


def procesar_rol(datos_rol, items, summ_por_key):
    d = datos_rol[0]
    partidas = d[6][1]
    victorias = d[6][0]
    inicio_ids = d[2][2]
    core_ids = d[3][2]
    secuencia = d[4][2]
    max_orden = d[4][3]
    summ_ids = d[1][2]

    build_ids = [i for i in core_ids]
    for slot in d[5][:3]:  # opciones del 4.º, 5.º y 6.º objeto
        op = mejor_opcion(items, slot, set(build_ids))
        if op:
            build_ids.append(op)

    return {
        "partidas": partidas,
        "winrate": round(victorias / partidas * 100, 1) if partidas else None,
        "ORDEN_HABILIDADES": ["R"] + list(max_orden),
        "secuencia_niveles": secuencia,
        # sin deduplicar: las pociones iniciales se repiten a propósito (ej: 2x poción)
        "objetos_iniciales": [
            {"id": i, "nombre": nombre_item(items, i)} for i in inicio_ids if nombre_item(items, i)
        ],
        "BUILD_OBJETIVO": lista_items(items, build_ids),
        "hechizos": [summ_por_key.get(str(s), str(s)) for s in summ_ids],
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", help="Versión de Data Dragon (por defecto la última)")
    ap.add_argument("--salida", default="lol_coach_data.json")
    ap.add_argument("--pausa", type=float, default=0.4, help="Segundos entre peticiones a u.gg")
    args = ap.parse_args()

    version = args.version or get_json(f"{DDRAGON}/api/versions.json")[0]
    patch_ugg = "_".join(version.split(".")[:2])
    print(f"Data Dragon {version} | u.gg parche {patch_ugg}")

    champs, items, summ = cargar_ddragon(version)
    summ_por_key = {s["key"]: s["name"] for s in summ.values()}

    # Tabla de objetos con activo (para objetos_activos del prompt)
    objetos_activos = {}
    for iid, it in items.items():
        if not it.get("gold", {}).get("purchasable", True) and "Trinket" not in it.get("tags", []):
            continue
        if it.get("maps", {}).get("11") is False:  # solo Grieta del Invocador
            continue
        t = tipo_activo(it)
        if t:
            objetos_activos[iid] = {"nombre": it["name"], "tipo": t, "descripcion": limpiar_html(it.get("plaintext") or "")}

    salida = {
        "version": version,
        "fuente": {
            "campeones_objetos": "Riot Data Dragon es_ES",
            "builds_habilidades": f"u.gg overview parche {patch_ugg}, todos los rangos, mundial",
        },
        "tecla_por_slot": TECLA_POR_SLOT,
        "objetos_activos": objetos_activos,
        "campeones": {},
    }

    errores = []
    total = len(champs)
    for n, (cid, c) in enumerate(sorted(champs.items(), key=lambda x: x[1]["name"]), 1):
        entrada = {
            "id": cid,
            "key": int(c["key"]),
            "nombre": c["name"],
            "titulo": c["title"],
            "clases": c["tags"],
            "recurso": c["partype"],
            "dificultad": c["info"]["difficulty"],
            "roles": {},
        }
        try:
            data = get_json(UGG.format(patch=patch_ugg, champ_id=c["key"]))
            region = data.get(REGION_MUNDIAL, {})
            tier = region.get(TIER_TODOS) or region.get(TIER_RESPALDO) or {}
            total_partidas = sum(r[0][6][1] for r in tier.values()) or 1
            for rkey, rdata in tier.items():
                if rkey not in ROLES:
                    continue
                p = rdata[0][6][1]
                if p < MIN_PARTIDAS_ROL or p / total_partidas < MIN_CUOTA_ROL:
                    continue
                entrada["roles"][ROLES[rkey]] = procesar_rol(rdata, items, summ_por_key)
            # roles ordenados por popularidad
            entrada["roles"] = dict(sorted(entrada["roles"].items(), key=lambda x: -x[1]["partidas"]))
            entrada["rol_principal"] = next(iter(entrada["roles"]), None)
            if not entrada["roles"]:
                errores.append(f"{c['name']}: sin datos de rol suficientes")
        except Exception as e:
            errores.append(f"{c['name']}: {e}")
        salida["campeones"][cid] = entrada
        print(f"[{n}/{total}] {c['name']}: {', '.join(entrada['roles']) or 'SIN DATOS'}")
        time.sleep(args.pausa)

    with open(args.salida, "w", encoding="utf-8") as f:
        json.dump(salida, f, ensure_ascii=False, indent=2)
    print(f"\nGuardado {args.salida} ({len(salida['campeones'])} campeones)")
    if errores:
        print("Avisos:", *errores, sep="\n  ")
    return 0


if __name__ == "__main__":
    sys.exit(main())
