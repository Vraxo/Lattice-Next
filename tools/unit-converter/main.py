from __future__ import annotations

import json
import sys
from decimal import Decimal, DecimalException, localcontext
from typing import Any

PROTOCOL_VERSION = 1
MAX_DECIMAL_COEFFICIENT = 79228162514264337593543950335
MAX_DECIMAL_SCALE = 28

UNITS: dict[str, tuple[str, Decimal]] = {
    "mm": ("length", Decimal("0.001")),
    "cm": ("length", Decimal("0.01")),
    "m": ("length", Decimal("1")),
    "km": ("length", Decimal("1000")),
    "mg": ("mass", Decimal("0.001")),
    "g": ("mass", Decimal("1")),
    "kg": ("mass", Decimal("1000")),
    "ml": ("volume", Decimal("0.001")),
    "l": ("volume", Decimal("1")),
    "s": ("time", Decimal("1")),
    "min": ("time", Decimal("60")),
    "h": ("time", Decimal("3600")),
}


def read_decimal(value: Any, name: str) -> Decimal:
    if isinstance(value, bool) or not isinstance(value, (Decimal, int)):
        raise ValueError(f"'{name}' must be a JSON number.")

    number = value if isinstance(value, Decimal) else Decimal(value)
    sign, digits, exponent = number.as_tuple()
    coefficient = int("".join(str(digit) for digit in digits) or "0")

    while exponent < 0 and coefficient and coefficient % 10 == 0:
        coefficient //= 10
        exponent += 1

    scale = max(0, -exponent)
    if scale > MAX_DECIMAL_SCALE:
        raise ValueError(f"'{name}' exceeds the supported decimal scale.")

    if exponent > 0:
        coefficient *= 10**exponent

    if coefficient > MAX_DECIMAL_COEFFICIENT:
        raise ValueError(f"'{name}' is outside the supported decimal range.")

    return number


def format_decimal(value: Decimal) -> str:
    formatted = format(value, "f")
    if "." in formatted:
        formatted = formatted.rstrip("0").rstrip(".")
    return "0" if formatted in ("", "-0") else formatted


def write_success(request_id: Any, result: Decimal) -> None:
    result_json = format_decimal(result)
    request_id_json = json.dumps(request_id, separators=(",", ":"))
    sys.stdout.write(
        '{"protocolVersion":1,"requestId":'
        + request_id_json
        + ',"success":true,"result":'
        + result_json
        + "}\n"
    )


def write_failure(request_id: Any, message: str) -> None:
    response = {
        "protocolVersion": PROTOCOL_VERSION,
        "requestId": request_id,
        "success": False,
        "error": message,
    }
    sys.stdout.write(json.dumps(response, separators=(",", ":")) + "\n")


def execute(request: dict[str, Any]) -> Decimal:
    arguments = request.get("arguments")
    if not isinstance(arguments, dict):
        raise ValueError("'arguments' must be an object.")

    value = read_decimal(arguments.get("value"), "value")
    from_name = arguments.get("from")
    to_name = arguments.get("to")

    if not isinstance(from_name, str) or from_name.lower() not in UNITS:
        raise ValueError(f"Unsupported source unit '{from_name}'.")
    if not isinstance(to_name, str) or to_name.lower() not in UNITS:
        raise ValueError(f"Unsupported destination unit '{to_name}'.")

    from_dimension, from_factor = UNITS[from_name.lower()]
    to_dimension, to_factor = UNITS[to_name.lower()]

    if from_dimension != to_dimension:
        raise ValueError(
            "The source and destination units have incompatible dimensions."
        )

    with localcontext() as context:
        context.prec = 28
        try:
            result = value * from_factor / to_factor
        except DecimalException as error:
            raise ValueError(
                "The converted value is outside the supported decimal range."
            ) from error

    read_decimal(result, "result")
    return result


def main() -> None:
    request_id: Any = None

    try:
        line = sys.stdin.readline()
        if not line:
            raise ValueError("No request was supplied.")

        request = json.loads(
            line,
            parse_float=Decimal,
            parse_int=Decimal,
        )
        if not isinstance(request, dict):
            raise ValueError("The request must be a JSON object.")

        request_id = request.get("requestId")
        if request.get("protocolVersion") != PROTOCOL_VERSION:
            raise ValueError("Unsupported protocol version.")
        if request.get("operation") != "execute":
            raise ValueError("Unsupported operation.")

        write_success(request_id, execute(request))
    except (json.JSONDecodeError, DecimalException, ValueError, TypeError) as error:
        write_failure(request_id, str(error))
    except Exception:
        write_failure(request_id, "The unit converter encountered an unexpected error.")


if __name__ == "__main__":
    main()
