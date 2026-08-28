import * as vscode from "vscode";

const UNION_OPERATOR_TOKEN_TYPE = "unionOperator";

const legend = new vscode.SemanticTokensLegend(
    [UNION_OPERATOR_TOKEN_TYPE],
    []
);

/**
 * A semantic token provider that highlights `|` tokens in union-type syntax
 * (e.g. `string | int`) within C# files that live under a `Source/` directory.
 *
 * The heuristic: find `|` characters outside strings, char literals, and comments.
 * Only files whose path contains a `Source` directory segment are processed.
 */
class UnionTypeTokenProvider implements vscode.DocumentSemanticTokensProvider {
    provideDocumentSemanticTokens(
        document: vscode.TextDocument,
        token: vscode.CancellationToken
    ): vscode.SemanticTokens {
        const builder = new vscode.SemanticTokensBuilder(legend);

        if (!this.isUnionSourceFile(document.uri.fsPath)) {
            return builder.build();
        }

        const text = document.getText();
        const positions = this.findPipePositions(text);

        for (const pos of positions) {
            const lineInfo = document.positionAt(pos);
            const range = new vscode.Range(
                lineInfo.line, lineInfo.character,
                lineInfo.line, lineInfo.character + 1
            );
            builder.push(range, UNION_OPERATOR_TOKEN_TYPE, []);
        }

        return builder.build();
    }

    private isUnionSourceFile(filePath: string): boolean {
        const normalized = filePath.replace(/\\/g, "/");
        const segments = normalized.split("/");
        return segments.some((s) => s === "Source");
    }

    /**
     * Find all `|` character positions that are NOT inside string literals,
     * char literals, line comments, or block comments.
     */
    private findPipePositions(source: string): number[] {
        const positions: number[] = [];
        let inString = false;
        let inChar = false;
        let inLineComment = false;
        let inBlockComment = false;

        for (let i = 0; i < source.length; i++) {
            const c = source[i];
            const prev = i > 0 ? source[i - 1] : "\0";

            if (inLineComment) {
                if (c === "\n") inLineComment = false;
                continue;
            }

            if (inBlockComment) {
                if (c === "*" && i + 1 < source.length && source[i + 1] === "/") {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            if (inString) {
                if (c === '"' && prev !== "\\") inString = false;
                continue;
            }

            if (inChar) {
                if (c === "'" && prev !== "\\") inChar = false;
                continue;
            }

            // Check for comment starts
            if (c === "/" && i + 1 < source.length) {
                if (source[i + 1] === "/") {
                    inLineComment = true;
                    i++;
                    continue;
                }
                if (source[i + 1] === "*") {
                    inBlockComment = true;
                    i++;
                    continue;
                }
            }

            // Check for string/char starts
            if (c === '"') {
                inString = true;
                continue;
            }
            if (c === "'") {
                inChar = true;
                continue;
            }

            // Found a `|` outside any literal/comment
            if (c === "|") {
                positions.push(i);
            }
        }

        return positions;
    }
}

export function activate(context: vscode.ExtensionContext): void {
    const provider = new UnionTypeTokenProvider();

    const disposable = vscode.languages.registerDocumentSemanticTokensProvider(
        { scheme: "file", language: "csharp" },
        provider,
        legend
    );

    context.subscriptions.push(disposable);
}

export function deactivate(): void { }