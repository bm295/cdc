Generate a commit message for currently staged files.

Rules:
1. Inspect staged diff only.
2. If staged changes are new features, prefix subject with `(feature)`.
3. Return output in this exact format:
   - A single fenced code block
   - First line: commit subject
   - Blank line
   - Bullet list body lines
4. Do not include any text before or after the code block.
