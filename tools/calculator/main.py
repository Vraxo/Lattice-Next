from __future__ import annotations

import json
import sys
from decimal import Decimal, DecimalException, localcontext
from typing import Any

PROTOCOL_VERSION = 1
MAX_DECIMAL_COEFFICIENT = 79228162514264337593543950335
MAX_DECIMAL_SCALE = 28


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
        if len(digits) + exponent > 29:
            raise ValueError(f"'{name}' is outside the supported decimal range.")
        coefficient *= 10**exponent

    if coefficient > MAX_DECIMAL_COEFFICIENT:
        raise ValueError(f"'{name}' is outside the supported decimal range.")

    return number


def fit_result(value: Decimal, name: str) -> Decimal:
    if value.as_tuple().exponent < -MAX_DECIMAL_SCALE:
        with localcontext() as context:
            context.prec = 29
            value = value.quantize(Decimal("1e-28"))

    read_decimal(value, name)
    return value


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

    left = read_decimal(arguments.get("left"), "left")
    right = read_decimal(arguments.get("right"), "right")
    operation = arguments.get("operation")

    if not isinstance(operation, str):
        raise ValueError("'operation' must be a string.")

    if operation == "divide" and right == 0:
        raise ValueError("Division by zero is undefined.")

    with localcontext() as context:
        context.prec = 29
        try:
            if operation == "add":
                result = left + right
            elif operation == "subtract":
                result = left - right
            elif operation == "multiply":
                result = left * right
            elif operation == "divide":
                result = left / right
            else:
                raise ValueError(
                    "Operation must be add, subtract, multiply, or divide."
                )
        except DecimalException as error:
            raise ValueError("The operation is outside the supported decimal range.") from error

    return fit_result(result, "result")


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
        write_failure(request_id, "The calculator encountered an unexpected error.")


if __name__ == "__main__":
    main()
