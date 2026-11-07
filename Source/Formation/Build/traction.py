"""Primary traction builder. Local coordinates are X along the consist, Z up."""
from traction_engine import engine
from traction_tender import tender


def build(number,g,l):
    if number == 1:
        engine(g,l)
    elif number == 2:
        tender(g,l)
    else:
        raise ValueError("The traction section is defined for units 01 and 02.")
