#!/bin/sh
# pre-commit: 拦截过大的 C# 源文件（Unity 适配版）
#
# 为什么只查 .cs：
#   - 二进制资产（贴图/模型/预制体/音频）体积天然很大，按大小拆没有意义
#   - .meta 是 Unity 的 GUID 引用锚点，绝不能动
#   - 第三方包（Packages/、Assets/Art/）不归本项目管
#
# 阈值 20KB（约 5000 token）。超过说明这个文件 agent 每次都要读很久，
# 应该按职责拆成几个文件。

LIMIT=20000
FAIL=0

for f in $(git diff --cached --name-only --diff-filter=ACM); do
  case "$f" in
    *.cs) ;;
    *) continue ;;
  esac
  case "$f" in
    Packages/*) continue ;;
    Assets/Art/*) continue ;;
    .agents/*) continue ;;
    */obj/*|*/bin/*) continue ;;
  esac
  [ -f "$f" ] || continue
  SIZE=$(wc -c < "$f" 2>/dev/null | tr -d ' ')
  [ -z "$SIZE" ] && continue
  if [ "$SIZE" -gt "$LIMIT" ]; then
    KB=$((SIZE / 1024))
    echo "" >&2
    echo "✗ 文件过大：$f （${KB} KB，限制 $((LIMIT / 1024)) KB）" >&2
    echo "  这个文件 agent 每次都要整份读，太大了。" >&2
    echo "  请按职责拆成几个 .cs 文件，再重新提交。" >&2
    FAIL=1
  fi
done

if [ "$FAIL" -eq 1 ]; then
  echo "" >&2
  echo "提交被拦截。拆分后重试（git reset 可撤销暂存）。" >&2
  exit 1
fi

exit 0
