import MDEditor from '@uiw/react-md-editor'
import { Box, Typography } from '@mui/material'

interface MarkdownFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  height?: number
  required?: boolean
}

export function MarkdownField({ label, value, onChange, height = 220, required }: MarkdownFieldProps) {
  return (
    <Box data-color-mode="light">
      <Typography
        variant="caption"
        sx={{
          color: "text.secondary",
          mb: 0.5,
          display: 'block'
        }}>
        {label}{required ? ' *' : ''}
      </Typography>
      <MDEditor value={value} onChange={(val) => onChange(val ?? '')} height={height} />
    </Box>
  );
}
