# PEP 440: Version Specifier Reference

Quick reference for version specifier syntax used in PEP 723 `# /// script` dependency blocks.

## Quick Reference

| Pattern | Example | Matches | Doesn't Match |
|---------|---------|---------|---------------|
| `==` | `pandas==1.5.3` | 1.5.3 only | 1.5.2, 1.5.4, 2.0 |
| `!=` | `numpy!=2.0.0` | Any except 2.0.0 | 2.0.0 |
| `>` | `matplotlib>3.0` | 3.0.1, 3.5, etc. | 3.0.0, 2.9 |
| `<` | `scipy<2.0` | 1.9, 1.0, etc. | 2.0.0, 2.1 |
| `>=` | `sklearn>=1.0` | 1.0, 1.5, 2.0, etc. | 0.9, 0.8 |
| `<=` | `torch<=1.13` | 1.13, 1.12, 1.0, etc. | 1.13.1, 2.0 |
| `~=` | `pydantic~=2.0` | 2.0, 2.1, 2.99 | 2.100+, 3.0 |

## Example

```python
# /// script
# dependencies = [
#     "pandas>=1.5,<2.0",
#     "numpy~=1.24.0",
# ]
# ///
```

## More Information

- [PEP 440](https://peps.python.org/pep-0440/) — official version specifier specification
